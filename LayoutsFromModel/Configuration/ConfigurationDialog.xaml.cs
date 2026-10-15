using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Document;

namespace LayoutsFromModel.Configuration
{
    /// <summary>
    /// Interaction logic for ConfigurationDialog.xaml
    /// </summary>
    public partial class ConfigurationDialog : Window
    {
        public string Prefix { get; private set; }
        public string Suffix { get; private set; }
        public int? Precision { get; set; }
        public double? ReferenceDimension { get; set; }
        
        public string BlockName { get; set; }
        public string TagName { get; set; }
        public int? BlockRatioScale { get; set; }
        public bool LockViewPorts { get; set; }
        public bool ManualFrameWithPaperFormat { get; set; }
        
        /// <summary>
        /// Создаёт окно настроек.
        /// </summary>
        public ConfigurationDialog()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// Создаёт окно с текущими значениями настроек.
        /// </summary>
        public ConfigurationDialog
            (string prefix, string suffix, Nullable<int> precision,
            double referenceDimension,
            string blockname, string tagname, int blockRatioScale, bool lockViewPorts,
            bool manualFrameWithPaperFormat)
            :this()
        {
            this.Prefix = prefix;
            this.Suffix = suffix;
            this.Precision = precision;
            this.ReferenceDimension = referenceDimension;
            this.BlockName = blockname;
            this.TagName = tagname;
            this.BlockRatioScale = blockRatioScale;
            this.LockViewPorts = lockViewPorts;
            this.ManualFrameWithPaperFormat = manualFrameWithPaperFormat;
        }
        
        /// <summary>
        /// Заполняет элементы окна текущими настройками.
        /// </summary>
        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtPrefix.Text = this.Prefix;
            txtSuffix.Text = this.Suffix;
            txtPrecision.Text = this.Precision.ToString();
            txtBlockName.Text = this.BlockName;
            txtTagName.Text = this.TagName;
            txtBlockRatioScale.Text = this.BlockRatioScale.ToString();
            chkLockVP.IsChecked = this.LockViewPorts;
            chkManualFrameWithPaperFormat.IsChecked = this.ManualFrameWithPaperFormat;
        }

        /// <summary>
        /// Считывает значения элементов перед закрытием окна.
        /// </summary>
        void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Prefix = txtPrefix.Text.Trim();
            this.Suffix = txtSuffix.Text.Trim();

            int precision;
            this.Precision = int.TryParse(txtPrecision.Text.Trim(), out precision)
                            ? (int?)Math.Abs(precision)
                            : null;

            this.BlockName = txtBlockName.Text.Trim();
            this.TagName = txtTagName.Text.Trim();
            this.LockViewPorts = chkLockVP.IsChecked ?? false;
            this.ManualFrameWithPaperFormat = chkManualFrameWithPaperFormat.IsChecked ?? false;

            int outBlockRatioScale;
            this.BlockRatioScale = int.TryParse(txtBlockRatioScale.Text.Trim(), out outBlockRatioScale)
                                    ? (int?)Math.Abs(outBlockRatioScale) : 1;
        }

        /// <summary>
        /// Подтверждает изменения настроек.
        /// </summary>
        void OnButtonOkClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        /// <summary>
        /// Запрашивает вхождение блока и тег атрибута для номера листа.
        /// </summary>
        void OnSelectBlockNameClick(object sender, RoutedEventArgs e)
        {
            AcadDocument document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (document == null)
                return;

            string selectedBlockName = null;
            string selectedTagName = null;
            using (document.Editor.StartUserInteraction(this))
            {
                while (true)
                {
                    PromptEntityOptions blockOptions = new PromptEntityOptions("\nВыберите блок-рамку:");
                    blockOptions.SetRejectMessage("\nНеобходимо выбрать блок.");
                    blockOptions.AddAllowedClass(typeof(BlockReference), false);

                    PromptEntityResult blockResult = document.Editor.GetEntity(blockOptions);
                    if (blockResult.Status != PromptStatus.OK)
                        return;

                    List<string> tagNames = new List<string>();
                    HashSet<string> uniqueTagNames = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
                    using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
                    {
                        BlockReference blockReference = (BlockReference)transaction.GetObject(
                            blockResult.ObjectId, OpenMode.ForRead);
                        ObjectId definitionId = blockReference.IsDynamicBlock
                            ? blockReference.DynamicBlockTableRecord
                            : blockReference.BlockTableRecord;
                        BlockTableRecord definition = (BlockTableRecord)transaction.GetObject(
                            definitionId, OpenMode.ForRead);
                        selectedBlockName = definition.Name;

                        foreach (ObjectId attributeId in blockReference.AttributeCollection)
                        {
                            AttributeReference attribute = (AttributeReference)transaction.GetObject(
                                attributeId, OpenMode.ForRead);
                            if (!string.IsNullOrWhiteSpace(attribute.Tag) && uniqueTagNames.Add(attribute.Tag))
                                tagNames.Add(attribute.Tag);
                        }

                        transaction.Commit();
                    }

                    if (tagNames.Count == 0)
                    {
                        document.Editor.WriteMessage(
                            "\nДанный блок не имеет тегов для указания номера листа. Выберите другой блок.");
                        continue;
                    }

                    PromptKeywordOptions tagOptions = new PromptKeywordOptions(
                        "\nВыберите тег для указания номера листа:");
                    tagOptions.AllowNone = false;
                    foreach (string tagName in tagNames)
                        tagOptions.Keywords.Add(tagName);

                    PromptResult tagResult = document.Editor.GetKeywords(tagOptions);
                    if (tagResult.Status != PromptStatus.OK)
                        return;

                    selectedTagName = tagResult.StringResult;
                    break;
                }
            }

            txtBlockName.Text = selectedBlockName;
            txtTagName.Text = selectedTagName;
        }
    }
}
