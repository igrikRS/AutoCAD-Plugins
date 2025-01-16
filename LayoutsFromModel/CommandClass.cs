// Microsoft
using System;
using System.Collections.Generic;
using System.Diagnostics;

// Autodesk
using Autodesk.AutoCAD.Runtime;
using acad = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.EditorInput;

[assembly: CommandClass(typeof(LayoutsFromModel.CommandClass))]

namespace LayoutsFromModel
{
    /// <summary>
    /// Данный класс содержит методы для непосредственной работы с AutoCAD
    /// </summary>
    public class CommandClass
    {
        [CommandMethodAttribute("igrikCreateLayoutsFrames", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        [CommandMethodAttribute("bargLFM", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void LayoutFromUserInput()
        {
            CreateLayouts(new UserInputBordersBuilder());
        }

        [CommandMethod("bargLFBL", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void LayoutFromBlocks()
        {
            CreateLayouts(new BlocksBordersBuilder());
        }

        [CommandMethodAttribute("igrikCreateLayoutsSelect", CommandFlags.Modal | CommandFlags.NoPaperSpace)]
        public void LayoutFromUserInputBlocks()
        {
            CreateLayouts(new UserInputBlocksBordersBuilder());
        }

        [CommandMethod("igrikCreateLayoutsAuto", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        public void LayoutFromBlocksAuto()
        {
            CreateLayouts(new UserAutoBlocksBordersBuilder());
        }

        //[CommandMethod("igrikCreateLayoutsSpds", CommandFlags.Modal | CommandFlags.NoPaperSpace | CommandFlags.UsePickSet)]
        //public void LayoutFromSpdsFormatAuto()
        //{
        //    CreateLayouts(new UserSpdsFormatBordersBuilder());
        //}

        private void CreateLayouts(IBordersCollectionBuilder bordersBuilder)
        {
            InitialUserInteraction initial = new InitialUserInteraction();
            initial.GetInitialData();
            if (initial.InitialDataStatus == PromptResultStatus.Cancelled)
                return;
            initial.FillPlotInfoManager();
            bordersBuilder.InitialBorderIndex = initial.Index;
            DrawingBorders[] borders = bordersBuilder.GetDrawingBorders();
            if (borders.Length == 0)
            {
                acad.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\nНе выбран ни один чертёж");
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
                ed.Regen();
            }
        }

    }
}