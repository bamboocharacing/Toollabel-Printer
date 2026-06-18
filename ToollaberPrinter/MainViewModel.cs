using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DymoSDK.Implementations;
using DymoSDK.Interfaces;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows;
using System.Windows.Threading;
using DynamicData.Binding;
using System.ComponentModel;

namespace ToollaberPrinter
{
    public class ToolViewModel : ReactiveObject
    {
        private string _place;
        private string _tool;
        private string _length;
        private string _radius;
        private string _cornerRadius;
        private string _type;

        public string Place
        {
            get => _place;
            set => this.RaiseAndSetIfChanged(ref _place, value);
        }

        public string Tool
        {
            get => _tool;
            set => this.RaiseAndSetIfChanged(ref _tool, value);
        }

        public string Length
        {
            get => _length;
            set => this.RaiseAndSetIfChanged(ref _length, value);
        }

        public string Radius
        {
            get => _radius;
            set => this.RaiseAndSetIfChanged(ref _radius, value);
        }

        public string CornerRadius
        {
            get => _cornerRadius;
            set => this.RaiseAndSetIfChanged(ref _cornerRadius, value);
        }

        public string Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        public ReactiveCommand<Unit, Unit> PrintToolLabel { get; private set; }
        public MainViewModel ViewModel { get; private set; }

        public ToolViewModel(string entry, MainViewModel viewModel)
        {
            Regex placeRegex = new Regex(@"P[0-9]*");
            Regex toolRegex = new Regex(@"T[0-9.]*");
            Regex lengthRegex = new Regex(@"L[0-9.-]*");
            Regex radiusRegex = new Regex(@"R[0-9.-]*");
            Regex cornerRadiusRegex = new Regex(@"C[0-9.-]*");
            Regex typeRegex = new Regex(@"G[0-9.]*");


            Place = placeRegex.Match(entry).Value;
            Tool = toolRegex.Match(entry).Value;
            Length = lengthRegex.Match(entry).Value;
            Radius = radiusRegex.Match(entry).Value;
            CornerRadius = cornerRadiusRegex.Match(entry).Value;
            Type = typeRegex.Match(entry).Value;

            ViewModel = viewModel;

            PrintToolLabel = ReactiveCommand.Create(() => viewModel.PrintToolLabelAction(this));
        }
    }

    public class MainViewModel : ReactiveObject
    {
        private string _printerName;
        private DymoLabel _labelTemplate;
        private SourceList<ToolViewModel> _toolsDmu50T = new SourceList<ToolViewModel>();
        private SourceList<ToolViewModel> _toolsDmu50V = new SourceList<ToolViewModel>();
        public IObservableCollection<ToolViewModel> ToolsDMU50T { get; } = new ObservableCollectionExtended<ToolViewModel>();
        public IObservableCollection<ToolViewModel> ToolsDMU50V { get; } = new ObservableCollectionExtended<ToolViewModel>();

        private string _filePathDmu50T;
        private string _filePathDmu50V;
        
        private ToolViewModel _selectedToolDmu50T;
        private ToolViewModel _selectedToolDmu50V;

        public ToolViewModel SelectedToolDMU50T
        {
            get => _selectedToolDmu50T;
            set => this.RaiseAndSetIfChanged(ref _selectedToolDmu50T, value);
        }

        public ToolViewModel SelectedToolDMU50V
        {
            get => _selectedToolDmu50V;
            set => this.RaiseAndSetIfChanged(ref _selectedToolDmu50V, value);
        }

        public string PrinterName
        {
            get => _printerName;
            set => this.RaiseAndSetIfChanged(ref _printerName, value);
        }

        public DymoLabel LabelTemplate
        {
            get => _labelTemplate;
            set => this.RaiseAndSetIfChanged(ref _labelTemplate, value);
        }

        public string FilePathDMU50T
        {
            get => _filePathDmu50T;
            set => this.RaiseAndSetIfChanged(ref _filePathDmu50T, value);
        }

        public string FilePathDMU50V
        {
            get => _filePathDmu50V;
            set => this.RaiseAndSetIfChanged(ref _filePathDmu50V, value);
        }

        public ReactiveCommand<Unit, Unit> PrintToolLabelDMU50T { get; private set; }
        public ReactiveCommand<Unit, Unit> PrintToolLabelDMU50V { get; private set; }

        public MainViewModel()
        {
            FilePathDMU50T = @"C:\CNC\DMU50T1\DATA";
            FilePathDMU50V = @"C:\CNC\DMU50V1\DATA";

            _toolsDmu50T.Connect().Bind(ToolsDMU50T).Subscribe();
            _toolsDmu50V.Connect().Bind(ToolsDMU50V).Subscribe();

            InitializePrinter();
            InitializeFileWatcher();
            OpenFile();

            var canPrintDmu50T = this.WhenAny(x => x.SelectedToolDMU50T, x => x.Value != null);
            var canPrintDmu50V = this.WhenAny(x => x.SelectedToolDMU50V, x => x.Value != null);

            PrintToolLabelDMU50T = ReactiveCommand.Create(() => PrintToolLabelAction(SelectedToolDMU50T), canPrintDmu50T);
            PrintToolLabelDMU50V = ReactiveCommand.Create(() => PrintToolLabelAction(SelectedToolDMU50V), canPrintDmu50V);
        }

        private void InitializePrinter()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            IEnumerable<IPrinter> printers = DymoPrinter.Instance.GetPrinters();
            PrinterName = printers.FirstOrDefault(x => x.Name == "DYMO LabelWriter 450").Name;
            DymoLabel label = new DymoLabel();
            label.LoadLabelFromFilePath("ToolLabel.dymo");

            LabelTemplate = label;
        }

        private void InitializeFileWatcher()
        {
            FileSystemWatcher fileWatcher1 = new FileSystemWatcher(FilePathDMU50T);
            fileWatcher1.Filter = "TM.TM";
            fileWatcher1.NotifyFilter = NotifyFilters.LastWrite;

            fileWatcher1.Changed += FileWatcher_Changed;

            fileWatcher1.EnableRaisingEvents = true;


            FileSystemWatcher fileWatcher2 = new FileSystemWatcher(FilePathDMU50V);
            fileWatcher2.Filter = "TM.TM";
            fileWatcher2.NotifyFilter = NotifyFilters.LastWrite;

            fileWatcher2.Changed += FileWatcher_Changed;

            fileWatcher2.EnableRaisingEvents = true;
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            OpenFile();
        }

        private void OpenFile()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _toolsDmu50T.Clear()));
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _toolsDmu50V.Clear()));

                foreach (string entry in File.ReadAllLines(Path.Combine(FilePathDMU50T, "TM.TM")).Where(x => x.StartsWith("P")))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _toolsDmu50T.Add(new ToolViewModel(entry, this))));
                }

                foreach (string entry in File.ReadAllLines(Path.Combine(FilePathDMU50V, "TM.TM")).Where(x => x.StartsWith("P")))
                {
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _toolsDmu50V.Add(new ToolViewModel(entry, this))));
                }
            }
            catch
            {

            }
        }

        public void PrintToolLabelAction(ToolViewModel tool)
        {
            ILabelObject toolLabel = LabelTemplate.GetLabelObject("TOOL");
            ILabelObject lengthLabel = LabelTemplate.GetLabelObject("LENGTH");
            ILabelObject radiusLabel = LabelTemplate.GetLabelObject("RADIUS");
            ILabelObject cornerRadiusLabel = LabelTemplate.GetLabelObject("CORNERRADIUS");
            ILabelObject typeLabel = LabelTemplate.GetLabelObject("TYPE");

            LabelTemplate.UpdateLabelObject(toolLabel, tool.Tool);
            LabelTemplate.UpdateLabelObject(lengthLabel, tool.Length);
            LabelTemplate.UpdateLabelObject(radiusLabel, tool.Radius);
            LabelTemplate.UpdateLabelObject(cornerRadiusLabel, tool.CornerRadius);
            LabelTemplate.UpdateLabelObject(typeLabel, tool.Type);

            DymoPrinter.Instance.PrintLabel(LabelTemplate, PrinterName, 1, false, false, 0, false, true);
        }
    }
}
