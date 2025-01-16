/*
 * Description:    Автоматическое создание листов из вхождений блоков на чертеже (только в пространстве модели):
 *                  - все создаваемые листы сортируются в возрастающем порядке по номеру листа (атрибут в блоке)
 *                  - блоки с пустым атрибутом    не используются в создании листов
 *                  - блоки на непечатаемых слоях не используются в создании листов
 *                  - имя листа автоматически берётся из атрибута в блоке
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

//using Multicad;

/*
namespace LayoutsFromModel
{
    /// <summary>
    /// Класс, создающий коллекцию границ чертежей из вхождений блоков
    /// </summary>
    /// команда "igrikCreateLayoutsAuto"

    public class UserSpdsFormatBordersBuilder : IBordersCollectionBuilder
    {
        private Database _wdb = HostApplicationServices.WorkingDatabase;

        public int InitialBorderIndex { get; set; }

        public string SpdsFormatObjectClassName = "mcsDbObjectFormat";
        public string SpdsFormatPropertySheetName = "Sheet";

        /// <summary>
        /// Получение границ из вхождений блоков
        /// </summary>
        /// <returns>Массив границ чертежей</returns>
        public DrawingBorders[] GetDrawingBorders()
        {
            List<DrawingBorders> borders = new List<DrawingBorders>();

            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                // Получаем коллекцию ObjectId вхождений блока blockname, затем сортируем
                IEnumerable<ObjectId> blockRefIds = null;

                Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
                PromptSelectionResult res = ed.SelectImplied();

                // если пользователем выбраны объекты до ввода команды
                // ищем экземпляры блока только из пользоват.выборки
                if (res.Status == PromptStatus.OK)
                {
                    LayerTable lt = tr.GetObject(_wdb.LayerTableId, OpenMode.ForRead) as LayerTable;

                    blockRefIds = res.Value
                        .GetObjectIds()
                        .Where(id => id.ObjectClass.Name == SpdsFormatObjectClassName)
                        .Where(id => IsBlockHasAttribute(tr, tagname, (BlockReference)tr.GetObject(id, OpenMode.ForRead)));
                        // .Where(id => ((LayerTableRecord)tr.GetObject(lt[id.Layer], OpenMode.ForRead)).IsPlottable );
                }
                else
                {
                    // сбор всех существующих "экземпляров" блока по его "типу класса" в модели
                    blockRefIds = GetBlockAllReferences(btrId);
                }

                // выборка блоков и сортировка
                blockRefIds = blockRefIds
                    .Select(n => (BlockReference)tr.GetObject(n, OpenMode.ForRead))
                    .OrderBy(n => AlphanumericCompare(GetBlockAttribute(tr, SpdsFormatPropertySheetName, n)))
                    .Select(n => n.ObjectId);

                int borderIndex = InitialBorderIndex;

                foreach (var brefId in blockRefIds)
                {
                    // получаем название листа из его тега ЛИСТ
                    // добавляем префикс и суффикс
                    string borderName = string.Format("{0}{1}{2}",
                                  Configuration.AppConfig.Instance.Prefix,
                                  GetBlockAttribute(tr, SpdsFormatPropertySheetName, (BlockReference)tr.GetObject(brefId, OpenMode.ForRead)),
                                  Configuration.AppConfig.Instance.Suffix);


                    // создаём рамку будущего листа
                    borders.Add(CreateBorder(brefId, borderName));
                }

                tr.Commit();
            }

            return borders.ToArray();
        }


        private List<ObjectId> GetBlockAllReferences(ObjectId blockId)
        {
            // создаём список (пустой)
            List<ObjectId> result = null;
            // открываем транзакцию
            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                // получаем указатель на таблицу копий блоков в конкретном месте (будем получать из модели)
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                // указатель на общую таблицу блоков в чертеже (в базе данных чертежа)
                BlockTable bt = (BlockTable)tr.GetObject(_wdb.BlockTableId, OpenMode.ForRead);
                ObjectId modelId = ((BlockTableRecord)tr
                                    .GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)).ObjectId;

                LayerTable lt = tr.GetObject(_wdb.LayerTableId, OpenMode.ForRead) as LayerTable;

                // собираем коллекцию блоков:
                // 1) получаем все вхождения
                // 2) сравниваем место расположения (n.OwnerId == modelId)
                // 3) получаем id объекта
                // 4) заносим в список тех, кто выполняет наши условия
                result = btr.GetAllBlockReferenceIds(true)
                    .Select(n => (BlockReference)tr.GetObject(n, OpenMode.ForRead))
                    // * итератор * * если в модели *    * блок динамический *                   * не лежит на непечатаемом слое *
                    .Where(n => (n.OwnerId == modelId && ((LayerTableRecord)tr.GetObject(lt[n.Layer], OpenMode.ForRead)).IsPlottable) )
                    .Select(n => n.ObjectId)
                    .ToList();
                tr.Commit();
            }
            return result;
        }

        /// <summary>
        /// Создание объекта границы блока-рамки
        /// Масштаб берётся из масштаба вхождения блока по оси X
        /// </summary>
        /// <param name="brefId">ObjectId вхождения блока рамки</param>
        /// <param name="name">Имя будущего листа</param>
        /// <returns>Объект границ чертежа</returns>
        private DrawingBorders CreateBorder(ObjectId brefId, string name)
        {
            DrawingBorders border = null;

            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                // получаем коэффициент масштаба блоков из диалога настроек
                int blockRatioScale = Configuration.AppConfig.Instance.BlockRatioScale;
                if (blockRatioScale < 1 || blockRatioScale > 1000)
                {
                    blockRatioScale = 1;
                    Configuration.AppConfig.Instance.BlockRatioScale = blockRatioScale;
                }

                BlockReference bref = (BlockReference)tr.GetObject(brefId, OpenMode.ForRead);
                double scale = bref.ScaleFactors.X * blockRatioScale;
                border = DrawingBorders.CreateDrawingBorders(bref.GeometricExtents.MinPoint,
                                                             bref.GeometricExtents.MaxPoint,
                                                             name,
                                                             scale);
                tr.Commit();
            }
            return border;
        }

        /// <summary>
        /// Получение текста заданного атрибута в указанном экземпляре блока
        /// </summary>
        /// <param name="tr">Транзакция</param>
        /// <param name="tagName">Имя искомого атрибута</param>
        /// <param name="blockRef">Указатель на блок-рамку</param>
        /// <returns></returns>
        public string GetBlockAttribute(Transaction tr, string tagName, BlockReference blockRef)
        {
            // цикл по всем атрибутам блока
            foreach (ObjectId id in blockRef.AttributeCollection)
            {
                // указатель на атрибут объекта по id
                var attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);

                // сравниваем текущий атрибут с искомым tagName
                if (attRef.Tag.Equals(tagName, StringComparison.CurrentCultureIgnoreCase))
                    return attRef.TextString.Replace("\"", "");
            }

            //McObjectId mcsId = Multicad.McObjectId.FromOldIdPtr(brefId.OldIdPtr);
            //McPropertySource mcPropertySource = mcsId.GetObject()?.Cast<McPropertySource>();
            //McProperties _mcProperties = mcPropertySource.ObjectProperties;
            //string sDrawingName = (string)_mcProperties.GetValueEx("Sheet", "");

            return "";
        }

        /// <summary>
        /// Сортировка строковых номеров листов
        /// © https://stackoverflow.com/questions/5093842/alphanumeric-sorting-using-linq
        /// </summary>
        /// <param name="input">Коллекция листов в строковом виде</param>
        /// <returns></returns>
        public static string AlphanumericCompare(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, "[0-9.]+", match => match.Value.PadLeft(10, '0'));
        }
    }
}
*/
