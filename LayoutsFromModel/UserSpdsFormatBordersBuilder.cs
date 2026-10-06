using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Multicad;

using McDbEntity = Multicad.DatabaseServices.McDbEntity;

namespace LayoutsFromModel
{
    /// <summary>
    /// Создаёт коллекцию границ чертежей из форматок СПДС GraphiCS.
    /// </summary>
    /// <remarks>Команда: igrikCreateLayoutsSpds.</remarks>
    public class UserSpdsFormatBordersBuilder : IBordersCollectionBuilder
    {
        private const string SpdsFormatObjectClassName = "mcsDbObjectFormat";
        private const string SpdsFormatPropertySheetName = "Sheet";

        private readonly Database _wdb = HostApplicationServices.WorkingDatabase;
        private readonly Editor _editor = Autodesk.AutoCAD.ApplicationServices.Application
            .DocumentManager.MdiActiveDocument.Editor;

        public int InitialBorderIndex { get; set; }

        /// <summary>
        /// Получает форматки из предварительного выбора или из пространства модели.
        /// </summary>
        public DrawingBorders[] GetDrawingBorders()
        {
            var borders = new List<DrawingBorders>();

            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                ObjectId modelSpaceId = GetModelSpaceId(tr);
                IEnumerable<ObjectId> formatIds = GetFormatIds(tr, modelSpaceId);
                var formats = new List<SpdsFormatInfo>();

                try
                {
                    foreach (ObjectId formatId in formatIds)
                    {
                        SpdsFormatInfo format;
                        if (TryReadFormat(tr, formatId, out format))
                        {
                            formats.Add(format);
                        }
                    }
                }
                catch (FileNotFoundException ex)
                {
                    WriteMultiCadLoadError(ex.Message);
                    return borders.ToArray();
                }
                catch (TypeLoadException ex)
                {
                    WriteMultiCadLoadError(ex.Message);
                    return borders.ToArray();
                }
                catch (BadImageFormatException ex)
                {
                    WriteMultiCadLoadError(ex.Message);
                    return borders.ToArray();
                }

                Configuration.AppConfig cfg = Configuration.AppConfig.Instance;

                foreach (SpdsFormatInfo format in formats
                    .OrderBy(item => CompareHelper.AlphanumericCompare(item.SheetNumber)))
                {
                    string borderName = string.Format(
                        "{0}{1}{2}",
                        cfg.Prefix,
                        format.SheetNumber,
                        cfg.Suffix);

                    DrawingBorders border = DrawingBorders.CreateDrawingBorders(
                        format.Extents.MinPoint,
                        format.Extents.MaxPoint,
                        borderName,
                        format.Scale);

                    borders.Add(border);
                    _editor.WriteMessage(
                        "\nДобавляем лист {0}. Формат листа: {1}. Масштаб: {2}",
                        borderName,
                        border.PSInfo.Name,
                        format.Scale);
                }

                tr.Commit();
            }

            if (borders.Count == 0)
            {
                _editor.WriteMessage("\nФорматки СПДС с заполненным свойством Sheet не найдены.\n");
            }

            return borders.ToArray();
        }

        /// <summary>
        /// Возвращает идентификатор пространства модели.
        /// </summary>
        private ObjectId GetModelSpaceId(Transaction tr)
        {
            var blockTable = (BlockTable)tr.GetObject(_wdb.BlockTableId, OpenMode.ForRead);
            return blockTable[BlockTableRecord.ModelSpace];
        }

        /// <summary>
        /// Возвращает подходящие форматки СПДС.
        /// </summary>
        private IEnumerable<ObjectId> GetFormatIds(Transaction tr, ObjectId modelSpaceId)
        {
            PromptSelectionResult selection = _editor.SelectImplied();
            IEnumerable<ObjectId> objectIds;

            if (selection.Status == PromptStatus.OK)
            {
                objectIds = selection.Value.GetObjectIds();
            }
            else
            {
                var modelSpace = (BlockTableRecord)tr.GetObject(modelSpaceId, OpenMode.ForRead);
                objectIds = modelSpace.Cast<ObjectId>();
            }

            return objectIds
                .Where(id => !id.IsNull && id.IsValid && !id.IsErased)
                .Where(id => id.ObjectClass.Name == SpdsFormatObjectClassName)
                .Where(id => IsUsableModelSpaceEntity(tr, id, modelSpaceId))
                .ToArray();
        }

        /// <summary>
        /// Проверяет расположение объекта и печатаемость его слоя.
        /// </summary>
        private bool IsUsableModelSpaceEntity(Transaction tr, ObjectId objectId, ObjectId modelSpaceId)
        {
            var entity = tr.GetObject(objectId, OpenMode.ForRead, false) as Entity;
            if (entity == null || entity.OwnerId != modelSpaceId)
            {
                return false;
            }

            var layer = tr.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
            return layer == null || layer.IsPlottable;
        }

        /// <summary>
        /// Читает номер листа, границы и масштаб форматки СПДС.
        /// </summary>
        private bool TryReadFormat(Transaction tr, ObjectId formatId, out SpdsFormatInfo format)
        {
            format = null;

            var entity = tr.GetObject(formatId, OpenMode.ForRead, false) as Entity;
            if (entity == null)
            {
                return false;
            }

            string sheetNumber;
            double multiCadScale;

            try
            {
                ReadMultiCadProperties(formatId, out sheetNumber, out multiCadScale);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (TypeLoadException)
            {
                throw;
            }
            catch (BadImageFormatException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _editor.WriteMessage(
                    "\nНе удалось прочитать свойства форматки СПДС {0}: {1}",
                    formatId.Handle,
                    ex.Message);
                return false;
            }

            sheetNumber = NormalizeSheetNumber(sheetNumber);
            if (string.IsNullOrEmpty(sheetNumber))
            {
                _editor.WriteMessage(
                    "\nПропущена форматка СПДС {0}: свойство Sheet не заполнено.",
                    formatId.Handle);
                return false;
            }

            Extents3d extents;
            try
            {
                extents = entity.GeometricExtents;
            }
            catch (Exception ex)
            {
                _editor.WriteMessage(
                    "\nПропущена форматка СПДС {0}: не удалось получить границы ({1}).",
                    formatId.Handle,
                    ex.Message);
                return false;
            }

            double scale = IsValidScale(multiCadScale)
                ? multiCadScale
                : entity.LinetypeScale;

            if (!IsValidScale(scale))
            {
                _editor.WriteMessage(
                    "\nУ форматки СПДС {0} некорректный масштаб. Используется масштаб 1.",
                    formatId.Handle);
                scale = 1.0;
            }

            format = new SpdsFormatInfo(sheetNumber, extents, scale);
            return true;
        }

        /// <summary>
        /// Метод вынесен отдельно, чтобы ошибка загрузки MultiCAD могла быть обработана вызывающим кодом.
        /// </summary>
        private static void ReadMultiCadProperties(
            ObjectId formatId,
            out string sheetNumber,
            out double scale)
        {
            sheetNumber = string.Empty;
            scale = double.NaN;

            McObjectId mcObjectId = McObjectId.FromOldIdPtr(formatId.OldIdPtr);
            McObject mcObject = mcObjectId.GetObject();
            if (mcObject == null)
            {
                return;
            }

            McPropertySource propertySource = mcObject.Cast<McPropertySource>();
            if (propertySource != null)
            {
                object sheetValue = propertySource.ObjectProperties.GetValueEx(
                    SpdsFormatPropertySheetName,
                    string.Empty);
                sheetNumber = Convert.ToString(sheetValue);
            }

            McDbEntity dbEntity = mcObject.Cast<McDbEntity>();
            if (dbEntity != null)
            {
                scale = dbEntity.Scale;
            }
        }

        /// <summary>
        /// Нормализует номер листа форматки СПДС.
        /// </summary>
        private static string NormalizeSheetNumber(string sheetNumber)
        {
            return string.IsNullOrWhiteSpace(sheetNumber)
                ? string.Empty
                : sheetNumber.Replace("\"", string.Empty).Trim();
        }

        /// <summary>
        /// Проверяет корректность масштаба форматки.
        /// </summary>
        private static bool IsValidScale(double scale)
        {
            return !double.IsNaN(scale) && !double.IsInfinity(scale) && scale > 0.0;
        }

        /// <summary>
        /// Выводит ошибку загрузки MultiCAD в командную строку.
        /// </summary>
        private void WriteMultiCadLoadError(string details)
        {
            _editor.WriteMessage(
                "\nНе удалось загрузить библиотеки MultiCAD. " +
                "Установите совместимую версию СПДС GraphiCS или Object Enabler.\n{0}\n",
                details);
        }

        private sealed class SpdsFormatInfo
        {
            /// <summary>
            /// Создаёт данные форматки СПДС.
            /// </summary>
            public SpdsFormatInfo(string sheetNumber, Extents3d extents, double scale)
            {
                SheetNumber = sheetNumber;
                Extents = extents;
                Scale = scale;
            }

            public string SheetNumber { get; private set; }
            public Extents3d Extents { get; private set; }
            public double Scale { get; private set; }
        }
    }
}
