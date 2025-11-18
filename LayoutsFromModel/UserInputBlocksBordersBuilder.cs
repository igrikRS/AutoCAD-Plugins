using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
//using Multicad;

using CO = LayoutsFromModel.Properties.CmdOptions;

namespace LayoutsFromModel
{
    /// <summary>
    /// Класс, создающий коллекцию границ чертежей с помощью
    /// поочерёдного выбора блоков-форматок
    /// </summary>
    /// команда "igrikCreateLayoutsSelect"

    public class UserInputBlocksBordersBuilder : IBordersCollectionBuilder
    {
        Editor ed;
        public int InitialBorderIndex { get; set; }

        private Database _wdb = HostApplicationServices.WorkingDatabase;

        public UserInputBlocksBordersBuilder()
        {
            ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\nВыбрано ручное создание листов.\n");
        }

        public DrawingBorders[] GetDrawingBorders()
        {
            List<DrawingBorders> borders = new List<DrawingBorders>();

            Configuration.AppConfig cfg = Configuration.AppConfig.Instance;

            BorderDrawer drawer = new BorderDrawer();

            using (Transaction tr = _wdb.TransactionManager.StartTransaction())
            {
                // Крутимся, пока нужны новые рамки
                bool needNewBorder = true;
                while (needNewBorder)
                {
                    // крутимся пока не выбран блок 
                    // (все другие типы объектов не пропускаем)
                    bool isBlock;
                    BorderPromptResult borderRes;
                    do
                    {
                        isBlock = true;
                        borderRes = GetBlockBorder(tr, ref isBlock);
                    } while (!isBlock);

                    switch (borderRes.QueryStatus)
                    {
                        // Использованы параметры ком. строки
                        case PromptResultStatus.Keyword:

                            // Запускаем процесс создания листов
                            if (borderRes.StringResult.Equals(CO.Process, StringComparison.InvariantCulture))
                                needNewBorder = false;

                            // Отменяем последний введённый чертёж
                            if (borderRes.StringResult.Equals(CO.Undo, StringComparison.InvariantCulture))
                            {
                                if (borders.Count > 0)
                                {
                                    borders.RemoveAt(borders.Count - 1);
                                    InitialBorderIndex--;

                                    drawer.ClearData();
                                    ed.Regen();

                                    foreach (DrawingBorders b in borders)
                                    {
                                        b.Accept(drawer);
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\nНечего возвращать");
                                }
                            }

                            // Выходим из команды
                            if (borderRes.StringResult.Equals(CO.Cancel, StringComparison.InvariantCulture))
                            {
                                ed.WriteMessage("\nОтмена!");
                                InitialBorderIndex = 0;
                                drawer.ClearData();
                                borders.Clear();
                                tr.Commit();
                                ed.Regen();
                                return borders.ToArray();
                            }
                            break;

                        case PromptResultStatus.OK:
                            // Введены точки
                            string bordername = string.Format("{0}{1}{2}", cfg.Prefix, InitialBorderIndex++, cfg.Suffix);
                            DrawingBorders border =
                                DrawingBorders.CreateDrawingBorders(borderRes.FirstPoint,
                                                                    borderRes.SecondPoint,
                                                                    bordername,
                                                                    borderRes.GetScale);
                            ed.WriteMessage("\nДобавляем лист {0}. Формат листа: {1}", bordername, border.PSInfo.Name);
                            borders.Add(border);
                            border.Accept(drawer);

                            break;

                        case PromptResultStatus.Cancelled:
                            // Пользователь нажал escape: отменяем все рамки
                            ed.WriteMessage("\nОтмена! (кнопкой)");
                            needNewBorder = false;

                            if (borders.Count > 0)
                            {
                                InitialBorderIndex = 0;
                                drawer.ClearData();
                            }

                            borders.Clear();
                            ed.Regen();
                            break;

                        default:
                            throw new System.Exception("Invalid value for BorderPromptResultStaus in file UserInputBlocksBordersBuilder.cs");
                    }
                }

                if (borders.Count > 0)
                {
                    InitialBorderIndex = 0;
                    drawer.ClearData();
                }

                tr.Commit();
            }

            return borders.ToArray();
        }


        BorderPromptResult GetBlockBorder(Transaction tr, ref bool isBlock)
        {
            PromptEntityOptions opt = new PromptEntityOptions("\nУкажите рамку формата листа:");

            opt.Keywords.Add(CO.Process);
            opt.Keywords.Add(CO.Undo);
            opt.Keywords.Add(CO.Cancel);
            // opt.AppendKeywordsToMessage = true;
            // opt.AllowNone = true;
            // opt.Keywords.Add("Set value");
            // opt.Keywords.Add("30");
            // opt.Keywords.Default = "30";

            // Запрашиваем выбор любого объекта
            // Но будем пропускать только блок
            PromptEntityResult rs = ed.GetEntity(opt);

            if (rs.Status == PromptStatus.OK)
            {
                ObjectId brefId = rs.ObjectId;

                if (brefId.ObjectClass.Name == "AcDbBlockReference") // блок
                    return GetBlockBorder(tr, brefId);
                else if (brefId.ObjectClass.Name == "mcsDbObjectFormat") // рамка СПДС
                    return GetSPDSFormatBorder(tr, brefId);
                else
                {
                    ed.WriteMessage("\nНеверно! Нужно выбрать БЛОК-рамку или СПДС-рамку!");
                    isBlock = false; // был выбран не тот объект - будем запрашивать выбор блока заново
                }
            }
            else if (rs.Status == PromptStatus.Keyword)
            {
                return new BorderPromptResult(rs.StringResult);
            }
            else if (rs.Status == PromptStatus.None)
            {
                isBlock = false;
            }
            else
            {
                // 
                isBlock = false;
            }

            return new BorderPromptResult();
        }


        // Функция получения рамки из блока-рамки
        // Важно!!! Атрибуты и дин. параметры не должны вылезать за пределы геометрии рамки
        BorderPromptResult GetBlockBorder(Transaction tr, ObjectId brefId)
        {
            BlockReference bref = (BlockReference)tr.GetObject(brefId, OpenMode.ForRead);

            // получаем коэффициент масштаба блоков из диалога настроек
            int blockRatioScale = Configuration.AppConfig.Instance.BlockRatioScale;
            if (blockRatioScale < 1 || blockRatioScale > 1000)
            {
                blockRatioScale = 1;
                Configuration.AppConfig.Instance.BlockRatioScale = blockRatioScale;
            }

            double scale = bref.ScaleFactors.Y * blockRatioScale;
            double listWidtht = (bref.GeometricExtents.MaxPoint.X - bref.GeometricExtents.MinPoint.X) / scale;
            double listhight = (bref.GeometricExtents.MaxPoint.Y - bref.GeometricExtents.MinPoint.Y) / scale;

            ed.WriteMessage("\nБЛОК-рамка: {0}x{1} (масштаб: {2})", System.Convert.ToInt32(listWidtht),
                                                                    System.Convert.ToInt32(listhight), scale);


            return new BorderPromptResult(bref.GeometricExtents.MinPoint,
                                            bref.GeometricExtents.MaxPoint,
                                            scale);
        }

        // функция получения рамки из формата СПДС
        BorderPromptResult GetSPDSFormatBorder(Transaction tr, ObjectId brefId)
        {
            Entity entity = (Entity)tr.GetObject(brefId, OpenMode.ForRead);

            /** получение данных о форматке:
             * 21 Разработал Author
             * 49 Проверил Control
             * 53 Название листа Drawing type
             * 69 Лист Sheet
             * 70 Листов SheetCount
             * 72 Стадия Stage
             * 73 Формат Format
             */
            //McObjectId mcsId = Multicad.McObjectId.FromOldIdPtr(brefId.OldIdPtr);
            ////McFormat _mcFormat = mcsId.GetObject()?.Cast<McFormat>(); // нужно найти библиотеку
            //McPropertySource mcPropertySource = mcsId.GetObject()?.Cast<McPropertySource>();
            //McProperties _mcProperties = mcPropertySource.ObjectProperties;
            //string sDrawingName = (string)_mcProperties.GetValueEx("Sheet", "");

            double scale = entity.LinetypeScale;
            double listWidtht = (entity.GeometricExtents.MaxPoint.X - entity.GeometricExtents.MinPoint.X) / scale;
            double listhight = (entity.GeometricExtents.MaxPoint.Y - entity.GeometricExtents.MinPoint.Y) / scale;

            ed.WriteMessage("\nСПДС-рамка: {0}x{1} (масштаб: {2})", System.Convert.ToInt32(listWidtht),
                                                                    System.Convert.ToInt32(listhight), scale);

            //ed.WriteMessage("\nСПДС-рамка: {0}x{1} (масштаб: {2}. Лист №{3})", System.Convert.ToInt32(listWidtht),
            //                                                        System.Convert.ToInt32(listhight), scale, sDrawingName);


            return new BorderPromptResult(entity.GeometricExtents.MinPoint,
                                            entity.GeometricExtents.MaxPoint,
                                            scale);
        }
    }
}

