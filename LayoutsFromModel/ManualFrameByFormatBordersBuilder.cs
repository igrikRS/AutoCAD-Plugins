using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Bargool.Acad.Library;

using CO = LayoutsFromModel.Properties.CmdOptions;
using CP = LayoutsFromModel.Properties.CmdPrompts;

namespace LayoutsFromModel
{
    /// <summary>
    /// Создаёт границы по двум точкам и выбранному пользователем формату.
    /// </summary>
    public class ManualFrameByFormatBordersBuilder : IBordersCollectionBuilder
    {
        private readonly Editor ed;

        public int InitialBorderIndex { get; set; }

        public ManualFrameByFormatBordersBuilder()
        {
            ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        }

        /// <summary>
        /// Получает указанные пользователем рамки.
        /// </summary>
        public DrawingBorders[] GetDrawingBorders()
        {
            List<DrawingBorders> borders = new List<DrawingBorders>();
            BorderDrawer drawer = new BorderDrawer();

            using (AcadSystemVariableSwitcher varSW = new AcadSystemVariableSwitcher("OSMODE", 1))
            {
                while (true)
                {
                    BorderPromptResult borderResult = GetBorderPoints();
                    if (borderResult.QueryStatus == PromptResultStatus.Cancelled)
                        break;

                    if (borderResult.QueryStatus == PromptResultStatus.Keyword)
                    {
                        if (borderResult.StringResult.Equals(CO.Process, StringComparison.InvariantCulture))
                            break;

                        if (borderResult.StringResult.Equals(CO.Undo, StringComparison.InvariantCulture))
                        {
                            UndoLastBorder(borders, drawer);
                            continue;
                        }

                        if (borderResult.StringResult.Equals(CO.Cancel, StringComparison.InvariantCulture))
                        {
                            ed.WriteMessage("\nОтмена!");
                            borders.Clear();
                            break;
                        }

                        continue;
                    }

                    if (borderResult.QueryStatus != PromptResultStatus.OK)
                        continue;

                    PaperFormatSelectionDialog formatDialog = new PaperFormatSelectionDialog();
                    if (formatDialog.ShowDialog() != true)
                        continue;

                    double width = Math.Abs(borderResult.FirstPoint.X - borderResult.SecondPoint.X);
                    double height = Math.Abs(borderResult.FirstPoint.Y - borderResult.SecondPoint.Y);
                    double scale = Math.Min(width, height) / formatDialog.SelectedShortSide;
                    if (scale <= 0)
                    {
                        ed.WriteMessage("\nРазмер рамки должен быть больше нуля");
                        continue;
                    }

                    Configuration.AppConfig cfg = Configuration.AppConfig.Instance;
                    string borderName = string.Format("{0}{1}{2}", cfg.Prefix, InitialBorderIndex++, cfg.Suffix);
                    DrawingBorders border = DrawingBorders.CreateDrawingBorders(
                        borderResult.FirstPoint,
                        borderResult.SecondPoint,
                        borderName,
                        scale);

                    ed.WriteMessage(
                        "\nДобавляем лист {0}. Масштабный коэффициент: {1:0.###}. Формат листа: {2}",
                        borderName,
                        scale,
                        border.PSInfo.Name);
                    borders.Add(border);
                    border.Accept(drawer);
                }
            }

            drawer.ClearData();
            return borders.ToArray();
        }

        private void UndoLastBorder(List<DrawingBorders> borders, BorderDrawer drawer)
        {
            if (borders.Count == 0)
            {
                ed.WriteMessage("\nНечего возвращать");
                return;
            }

            borders.RemoveAt(borders.Count - 1);
            InitialBorderIndex--;
            drawer.ClearData();
            foreach (DrawingBorders border in borders)
                border.Accept(drawer);
        }

        private BorderPromptResult GetBorderPoints()
        {
            PromptPointOptions firstPointOptions = new PromptPointOptions("\n" + CP.FrameFirstPointQuery);
            firstPointOptions.Keywords.Add(CO.Process);
            firstPointOptions.Keywords.Add(CO.Undo);
            firstPointOptions.Keywords.Add(CO.Cancel);

            PromptPointResult firstResult = ed.GetPoint(firstPointOptions);
            if (firstResult.Status == PromptStatus.Keyword)
                return new BorderPromptResult(firstResult.StringResult);
            if (firstResult.Status != PromptStatus.OK)
                return new BorderPromptResult(PromptResultStatus.Cancelled);

            Point3d firstPoint = firstResult.Value;
            PromptCornerOptions secondPointOptions = new PromptCornerOptions(CP.FrameOppositePointQuery, firstPoint)
            {
                UseDashedLine = true
            };
            PromptPointResult secondResult = ed.GetCorner(secondPointOptions);
            if (secondResult.Status != PromptStatus.OK)
                return new BorderPromptResult(PromptResultStatus.Cancelled);

            firstPoint = firstPoint.TransformBy(ed.CurrentUserCoordinateSystem);
            Point3d secondPoint = secondResult.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            return new BorderPromptResult(firstPoint, secondPoint);
        }
    }
}
