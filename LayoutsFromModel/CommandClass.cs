// Microsoft
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

// Autodesk
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using acad = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(LayoutsFromModel.CommandClass))]

namespace LayoutsFromModel
{
    /// <summary>
    /// Данный класс содержит методы для непосредственной работы с AutoCAD
    /// </summary>
    public class CommandClass
    {
        /// <summary>
        /// Открывает окно выбора способа создания листов.
        /// </summary>
        [CommandMethod("igrikCreateLayouts", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void OpenLayoutsCreationDialog()
        {
            while (true)
            {
                LayoutsCreationDialog dialog = new LayoutsCreationDialog();
                if (dialog.ShowDialog() != true)
                    return;

                IBordersCollectionBuilder bordersBuilder = null;
                bool requestInitialBorderIndex = true;
                switch (dialog.SelectedAction)
                {
                    case LayoutsCreationAction.AutomaticBlocks:
                        bordersBuilder = new UserAutoBlocksBordersBuilder();
                        requestInitialBorderIndex = false;
                        break;
                    case LayoutsCreationAction.AutomaticSpdsFormats:
                        bordersBuilder = new UserSpdsFormatBordersBuilder();
                        requestInitialBorderIndex = false;
                        break;
                    case LayoutsCreationAction.ManualBlocks:
                        bordersBuilder = new UserInputBlocksBordersBuilder();
                        break;
                    case LayoutsCreationAction.ManualFrame:
                        bordersBuilder = new UserInputBordersBuilder();
                        break;
                    case LayoutsCreationAction.DeleteLayouts:
                        System.Windows.MessageBoxResult confirmation = System.Windows.MessageBox.Show(
                            "Вы уверены, что хотите удалить все существующие листы?",
                            "Удаление листов",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning,
                            System.Windows.MessageBoxResult.No);
                        if (confirmation == System.Windows.MessageBoxResult.Yes)
                            new LayoutCreator().DeleteExistingLayouts();
                        continue;
                    case LayoutsCreationAction.Settings:
                        Configuration.AppConfig.Instance.ShowDialog();
                        continue;
                    case LayoutsCreationAction.Template:
                        new InitialUserInteraction().SelectTemplate();
                        continue;
                    default:
                        return;
                }

                CreateLayouts(bordersBuilder, requestInitialBorderIndex);
                return;
            }
        }

        /// <summary>
        /// Открывает окно настроек создания листов.
        /// </summary>
        [CommandMethod("igrikCreateLayoutsOptions", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void OpenInitialConfigDialog()
        {
            Configuration.AppConfig.Instance.ShowDialog();
        }

        /// <summary>
        /// Создаёт листы по рамкам, указанным вручную.
        /// </summary>
        [CommandMethod("igrikCreateLayoutsFrames", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        [CommandMethod("bargLFM", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void LayoutFromUserInput()
        {
            CreateLayouts(new UserInputBordersBuilder());
        }

        /// <summary>
        /// Создаёт листы по выбранным блокам.
        /// </summary>
        [CommandMethod("bargLFBL", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void LayoutFromBlocks()
        {
            CreateLayouts(new BlocksBordersBuilder());
        }

        /// <summary>
        /// Создаёт листы по вручную выбранным блокам и форматкам.
        /// </summary>
        [CommandMethod("igrikCreateLayoutsSelect", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void LayoutFromUserInputBlocks()
        {
            CreateLayouts(new UserInputBlocksBordersBuilder());
        }

        /// <summary>
        /// Автоматически создаёт листы по блокам-рамкам.
        /// </summary>
        [CommandMethod("igrikCreateLayoutsAuto", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void LayoutFromBlocksAuto()
        {
            CreateLayouts(new UserAutoBlocksBordersBuilder());
        }

        /// <summary>
        /// Автоматически создаёт листы по форматкам СПДС.
        /// </summary>
        [CommandMethod("igrikCreateLayoutsSpds", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void LayoutFromSpdsFormatAuto()
        {
            CreateLayouts(new UserSpdsFormatBordersBuilder());
        }

        /// <summary>
        /// Создаёт листы по границам, полученным выбранным построителем.
        /// </summary>
        /// <param name="bordersBuilder">Построитель границ чертежей.</param>
        /// <param name="requestInitialBorderIndex">Нужно ли запрашивать номер первого листа.</param>
        private void CreateLayouts(IBordersCollectionBuilder bordersBuilder, bool requestInitialBorderIndex = true)
        {
            InitialUserInteraction initial = new InitialUserInteraction();
            initial.GetInitialData(requestInitialBorderIndex);
            if (initial.InitialDataStatus == PromptResultStatus.Cancelled)
                return;
            initial.FillPlotInfoManager();
            bordersBuilder.InitialBorderIndex = initial.index;
            DrawingBorders[] borders = bordersBuilder.GetDrawingBorders();
            if (borders.Length == 0)
            {
                acad.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nОтсутствуют элементы для создания листов");
                return;
            }

            Editor ed = acad.DocumentManager.MdiActiveDocument.Editor;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            LayoutCreator layoutCreator = new LayoutCreator();
            foreach (DrawingBorders border in borders)
            {
                layoutCreator.CreateLayout(border);
            }

            stopwatch.Stop();
            ed.WriteMessage("\nВремя выполнения: {0} с\n", stopwatch.ElapsedMilliseconds / 1000);

            Configuration.AppConfig cfg = Configuration.AppConfig.Instance;

            // Если в конфигурации отмечено "возвращаться в модель" - то переходим в модель
            if (cfg.TilemodeOn)
                acad.SetSystemVariable("TILEMODE", 1);

            // Если в конфигурации отмечено "удалять неинициализированные листы" - удаляем их
            if (cfg.DeleteNonInitializedLayouts)
            {
                layoutCreator.DeleteNoninitializedLayouts();
            }

            ed.Regen();
        }

    }
}
