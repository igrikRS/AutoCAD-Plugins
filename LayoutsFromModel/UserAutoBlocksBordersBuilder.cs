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


namespace LayoutsFromModel
{
    /// <summary>
    /// Класс, создающий коллекцию границ чертежей из вхождений блоков
    /// </summary>
    public class UserAutoBlocksBordersBuilder : IBordersCollectionBuilder
    {
        private Database _wdb = HostApplicationServices.WorkingDatabase;

        public int InitialBorderIndex { get; set; }

        /// <summary>
        /// Получение границ из вхождений блоков
        /// </summary>
        /// <returns>Массив границ чертежей</returns>
        public DrawingBorders[] GetDrawingBorders()
        {
            List<DrawingBorders> borders = new List<DrawingBorders>();
            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                .MdiActiveDocument.Editor;
            string blockname = string.Empty;
            string tagname = string.Empty;

            try
            {
                blockname = GetBordersBlockName();
                tagname = GetBordersTagName();

                using (Transaction tr = _wdb.TransactionManager.StartTransaction())
                {
                    IEnumerable<ObjectId> blockRefIds = null;

                    BlockTable bt = (BlockTable)tr.GetObject(_wdb.BlockTableId, OpenMode.ForRead);
                    if (!bt.Has(blockname))
                    {
                        ed.WriteMessage(
                            "\nБлоков с именем \"{0}\" на чертеже нет!\n",
                            blockname);
                        return borders.ToArray();
                    }
                    ObjectId btrId = bt[blockname];

                    PromptSelectionResult res = ed.SelectImplied();

                    if (res.Status == PromptStatus.OK)
                    {
                        blockRefIds = res.Value
                            .GetObjectIds()
                            .Where(id => id.ObjectClass.Name == "AcDbBlockReference")
                            .Where(id => ((BlockReference)tr.GetObject(id, OpenMode.ForRead)).DynamicBlockTableRecord == btrId)
                            .Where(id => IsBlockHasAttribute(tr, tagname, (BlockReference)tr.GetObject(id, OpenMode.ForRead)));
                    }
                    else
                    {
                        blockRefIds = GetBlockAllReferences(btrId);
                    }

                    blockRefIds = blockRefIds
                        .Select(n => (BlockReference)tr.GetObject(n, OpenMode.ForRead))
                        .OrderBy(n => CompareHelper.AlphanumericCompare(GetBlockAttribute(tr, tagname, n)))
                        .Select(n => n.ObjectId)
                        .ToArray();

                    foreach (var brefId in blockRefIds)
                    {
                        try
                        {
                            string borderName = string.Format("{0}{1}{2}",
                                        Configuration.AppConfig.Instance.Prefix,
                                        GetBlockAttribute(tr, tagname, (BlockReference)tr.GetObject(brefId, OpenMode.ForRead)),
                                        Configuration.AppConfig.Instance.Suffix);

                            borders.Add(CreateBorder(brefId, borderName));
                        }
                        catch (Exception ex)
                        {
                            ed.WriteMessage(
                                "\nПропущен блок {0}: {1}",
                                brefId.Handle,
                                ex.Message);
                        }
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage(
                    "\nНе удалось автоматически обработать блоки: {0}\n",
                    ex.Message);
                return borders.ToArray();
            }

            DrawingBorders[] bordersArray = borders.ToArray();

            if (bordersArray.Length < 1)
            {
                ed.WriteMessage($"\n\nБлоков с именем \"{blockname}\" и тегом \"{tagname}\" на чертеже нет!\n");
            }

            return bordersArray;
        }

        /// <summary>
        /// Возвращает настроенное имя блока-рамки.
        /// </summary>
        private string GetBordersBlockName()
        {
            string blockname = Configuration.AppConfig.Instance.BlockName;

            if (string.IsNullOrEmpty(blockname))
                throw new System.Exception("Не задано имя блок-рамки!");
            return blockname;
        }

        /// <summary>
        /// Возвращает настроенное имя атрибута номера листа.
        /// </summary>
        private string GetBordersTagName()
        {
            string tagname = Configuration.AppConfig.Instance.TagName;

            if (string.IsNullOrEmpty(tagname))
                throw new System.Exception("Не задано название атрибута в блок-рамке!");
            return tagname;
        }

        /// <summary>
        /// Возвращает подходящие вхождения блока из пространства модели.
        /// </summary>
        private List<ObjectId> GetBlockAllReferences(ObjectId blockId)
        {
            string tagname = GetBordersTagName();
            List<ObjectId> result = null;
            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                BlockTable bt = (BlockTable)tr.GetObject(_wdb.BlockTableId, OpenMode.ForRead);
                ObjectId modelId = ((BlockTableRecord)tr
                                    .GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)).ObjectId;

                LayerTable lt = tr.GetObject(_wdb.LayerTableId, OpenMode.ForRead) as LayerTable;

                result = btr.GetAllBlockReferenceIds(true)
                    .Select(n => (BlockReference)tr.GetObject(n, OpenMode.ForRead))
                    .Where(n => (n.OwnerId == modelId && ((LayerTableRecord)tr.GetObject(lt[n.Layer], OpenMode.ForRead)).IsPlottable
                                                                          && IsBlockHasAttribute(tr, tagname, n) ))
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
                int blockRatioScale = Configuration.AppConfig.Instance.BlockRatioScale;
                if (blockRatioScale < 1 || blockRatioScale > 1000)
                {
                    blockRatioScale = 1;
                    Configuration.AppConfig.Instance.BlockRatioScale = blockRatioScale;
                }

                BlockReference bref = (BlockReference)tr.GetObject(brefId, OpenMode.ForRead);
                double scale = bref.ScaleFactors.X * blockRatioScale;
                Extents3d geometricExtents = GetBlockGeometricExtents(bref);

                border = DrawingBorders.CreateDrawingBorders(geometricExtents.MinPoint,
                                                             geometricExtents.MaxPoint,
                                                             name,
                                                             scale);

                tr.Commit();
            }
            return border;
        }

        /// <summary>
        /// Получить размеры видимой части динамического блока
        /// </summary>
        /// <param name="br">Ссылка на блок</param>
        /// <returns></returns>
        private Extents3d GetBlockGeometricExtents(BlockReference br)
        {
            var extents = new Extents3d();
            using (var entitySet = new DBObjectCollection())
            {
                br.Explode(entitySet);
                foreach (Entity entity in entitySet)
                {
                    if (entity.Visible)
                    {
                        var bounds = entity.Bounds;
                        if (bounds.HasValue)
                        {
                            extents.AddExtents(bounds.Value);
                        }
                    }
                    entity.Dispose();
                }
            }
            return extents;
        }

        /// <summary>
        /// Проверка существания искомого атрибута в указанном экземпляре блока
        /// </summary>
        /// <param name="tr"></param>
        /// <param name="tagName"></param>
        /// <param name="blockRef"></param>
        /// <returns></returns>
        public bool IsBlockHasAttribute(Transaction tr, string tagName, BlockReference blockRef)
        {
            foreach (ObjectId id in blockRef.AttributeCollection)
            {
                var attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);

                if (attRef.Tag.Equals(tagName, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(attRef.TextString))
                        return true;
                }
            }

            return false;
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
            foreach (ObjectId id in blockRef.AttributeCollection)
            {
                var attRef = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);

                if (attRef.Tag.Equals(tagName, StringComparison.CurrentCultureIgnoreCase))
                    return attRef.TextString.Replace("\"", "");
            }

            return "";
        }
    }
}
