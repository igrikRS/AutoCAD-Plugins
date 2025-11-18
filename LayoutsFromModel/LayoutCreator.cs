using System;
using System.Text.RegularExpressions;
// using System.Xaml;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;


namespace LayoutsFromModel
{
    /// <summary>
    /// Класс для создания листов
    /// </summary>
    public class LayoutCreator
    {
        Database wdb;
        Editor ed;
        int counter;

        public LayoutCreator()
        {
            this.wdb = HostApplicationServices.WorkingDatabase;
            this.ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            counter = 1;
        }

        /// <summary>
        /// Метод создаёт Layout по заданным параметрам
        /// </summary>
        /// <param name="borders">Границы выделенной области в пространстве модели</param>
        public void CreateLayout(DrawingBorders borders)
        {
            // получаем название листа
            string layoutName = CheckLayoutName(borders.Name);
            // получаем/объявляем классы листа
            LayoutManager lm = LayoutManager.Current;
            Layout layout;
            // объявляем поворот листа 
            PlotRotation rotation = 0;

            using (Transaction tr = this.wdb.TransactionManager.StartTransaction())
            {
                try
                {
                    layout = tr.GetObject(lm.CreateLayout(layoutName), OpenMode.ForWrite) as Layout;
                }
                catch (System.Exception ex)
                {
                    throw new System.Exception(String.Format("Ошибка создания Layout {0}\n{1}", layoutName, ex.Message));
                }

                // получаем данные для создания настроек плоттера
                PlotSettings ps = ImportPlotSettings(borders, tr);
                // копируем их в созданные лист
                layout.CopyFrom(ps);
                // получаем данные о повороте листа
                rotation = ps.PlotRotation;

                tr.Commit();
            }


            // эту херню необходимо вынести из транзакции, иначе AutoCAD2016 получает Exception(eSetFailed)
            // © https://adn-cis.org/forum/index.php?topic=2723.msg12312#msg12312
            lm.CurrentLayout = layout.LayoutName;

            // получаем размеры листа
            double x = layout.PlotPaperSize.X;
            double y = layout.PlotPaperSize.Y;

            // если лист повёрнут: меняем координаты местами
            if (rotation == PlotRotation.Degrees090)
            {
                x = layout.PlotPaperSize.Y;
                y = layout.PlotPaperSize.X;
            }

            // пишем пользователю о создании листа и зуммируем чтобы выглядело эстетично
            this.ed.WriteMessage("\nСоздаётся лист \"{0}\" размером: {1}x{2} \n", layout.LayoutName, x, y);
            this.ed.ZoomWindow(new Point3d(0, 0, 0), new Point3d(x, y, 0));

            using (Transaction tr = this.wdb.TransactionManager.StartTransaction())
            {
                CreateViewport(layout, borders, tr);
                tr.Commit();
            }
        }

        /// <summary>
        /// Метод удаляет неинициализированные листы
        /// </summary>
        public void DeleteNoninitializedLayouts()
        {
            using (Transaction tr = wdb.TransactionManager.StartTransaction())
            {
                DBDictionary dic = (DBDictionary)tr.GetObject(wdb.LayoutDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry entry in dic)
                {
                    Layout layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                    if (!layout.ModelType && layout.GetViewports().Count == 0)
                    {
                        if (dic.Count > 1)
                        {
                            if (!dic.IsWriteEnabled)
                                dic.UpgradeOpen();
                            dic.Remove(entry.Value);
                            layout.UpgradeOpen();
                            layout.Erase(true);
                            Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager
                                .MdiActiveDocument.Editor.WriteMessage("\nУдаляю лист " + layout.LayoutName + Environment.NewLine);
                        }
                    }
                }
                tr.Commit();
            }
        }

        /// <summary>
        /// Проверка корректности желаемого имени листа
        /// Если лист с желаемым именем уже существует, к данному имени добавится "(1)"
        /// Если и это имя уже есть - цифра в скобках будет увеличиваться, пока не будет найден
        /// уникальный вариант. Также данная функция обрабатывает "грязные" символы
        /// </summary>
        /// <param name="inputName">Название листа</param>
        /// <returns>Корректное имя листа</returns>
        private string CheckLayoutName(string inputName)
        {
            string layoutName = Regex.Replace(inputName, @"(=|,|;|\@|:|\?|\*|\[|\]|<|>|#|""|%|\/|\||\\)", "");
            if (string.IsNullOrEmpty(layoutName)) layoutName = string.Format("Y{0}", this.counter);
            using (Transaction tr = this.wdb.TransactionManager.StartTransaction())
            {
                // Проверяем на наличие листа с указанным именем
                DBDictionary layoutsDic = (DBDictionary)tr.GetObject(wdb.LayoutDictionaryId, OpenMode.ForRead);
                if (layoutsDic.Contains(layoutName))
                {
                    // Если есть - добавляем номер в скобках, итерируем номер, пока имя не станет уникальным
                    int dublicateLayoutIndex = 1;
                    while (layoutsDic.Contains(string.Format("{0}({1})", layoutName, dublicateLayoutIndex)))
                    {
                        ++dublicateLayoutIndex;
                    }
                    layoutName = string.Format("{0}({1})", layoutName, dublicateLayoutIndex);
                }
                tr.Commit();
            }
            ++this.counter;
            return layoutName;
        }

        /// <summary>
        /// Метод добавляет из объекта границ чертежа новые
        /// именованые настройки печати в файл, если таковых там нет
        /// </summary>
        /// <param name="borders">Объект границ чертежа</param>
        /// <param name="tr">Текущая транзакция</param>
        /// <returns>Настройки печати, соответствующие границам чертежа</returns>
        private PlotSettings ImportPlotSettings(DrawingBorders borders, Transaction tr)
        {
            PlotSettings ps = new PlotSettings(false);
            ps.CopyFrom(borders.PSInfo.PSettings);

            DBDictionary psDict = (DBDictionary)tr.GetObject(this.wdb.PlotSettingsDictionaryId,
                                                             OpenMode.ForRead);
            if (!psDict.Contains(ps.PlotSettingsName))
            {
                psDict.UpgradeOpen();
                ps.AddToPlotSettingsDictionary(this.wdb);
                tr.AddNewlyCreatedDBObject(ps, true);
                psDict.DowngradeOpen();
            }
            return ps;
        }

        /// <summary>
        /// Метод создаёт Viewport на заданном Layout, в размер листа
        /// </summary>
        /// <param name="layout">Layout, на котором создаётся viewport</param>
        /// <param name="borders">Границы выделенной области в модели</param>
        private void CreateViewport(Layout layout, DrawingBorders borders, Transaction tr, bool isLastLayout = true)
        {
            int vpCount = layout.GetViewports().Count;
            if (vpCount == 0)
            {
                throw new System.Exception(String.Format("Layout {0} не инициализирован", layout.LayoutName));
                // Если еще нет ни одного Viewport у Layout, то нужно его инициализировать
                // layout.UpgradeOpen();
                // layout.Initialize();
                // layout.DowngradeOpen();
                // vpCount = layout.GetViewports().Count;
            }
            Viewport viewport;
            if (vpCount == 1)
            {
                BlockTableRecord paperSpace =
                    (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite) as BlockTableRecord;
                viewport = new Viewport();
                viewport.SetDatabaseDefaults();
                paperSpace.AppendEntity(viewport);
                tr.AddNewlyCreatedDBObject(viewport, true);
                viewport.On = isLastLayout; // вот это заставляет вьюпорты обновляться и создаваться очень-очень долго
            }
            else
            {
                ObjectId viewportId = layout.GetViewports()[vpCount - 1];
                if (viewportId.IsNull)
                    throw new System.Exception("Не удалось получить вьюпорт!");

                viewport = (Viewport)tr.GetObject(viewportId, OpenMode.ForWrite);
                if (viewport == null)
                    throw new System.Exception("Не удалось получить вьюпорт!");
            }
            // Высоту и ширину вьюпорта выставляем в размер выделенной области
            viewport.Height = borders.Height / borders.ScaleFactor;
            viewport.Width = borders.Width / borders.ScaleFactor;
            viewport.CenterPoint = new Point3d(viewport.Width / 2 + layout.PlotOrigin.X,
                                         viewport.Height / 2 + layout.PlotOrigin.Y,
                                         0);
            viewport.ViewTarget = new Point3d(0, 0, 0);
            viewport.ViewHeight = borders.Height;
            viewport.ViewCenter = new Point2d(borders.Center.X, borders.Center.Y);
            viewport.Locked = LayoutsFromModel.Configuration.AppConfig.Instance.LockViewPorts;
        }

    }
}
