using ICSharpCode.AvalonEdit.Document;
using JefimsIncredibleXsltTool.Lib;
using JefimsIncredibleXsltTool.Transformers;
using Microsoft.Win32;
using Saxon.Api;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.Xsl;
using JUST;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;

namespace JefimsIncredibleXsltTool
{
    public class MainViewModel : Observable
    {
        public Notifier Notifier = new Notifier(cfg =>
        {
            cfg.DisplayOptions.TopMost = false;
            cfg.PositionProvider = new WindowPositionProvider(
                parentWindow: Application.Current.MainWindow,
                corner: Corner.TopRight,
                offsetX: 10,
                offsetY: 10);

            cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                notificationLifetime: TimeSpan.FromSeconds(2),
                maximumNotificationCount: MaximumNotificationCount.FromCount(3));

            cfg.Dispatcher = Application.Current.Dispatcher;
        });

        private Document _document;
        private XsltProcessingMode _xsltProcessingMode = XsltProcessingMode.SAXON;
        private const string ProgramName = "Jefim's Incredible XSLT Tool";
        public event EventHandler OnTransformFinished;
        public ColorTheme ColorTheme
        {
            get => _colorTheme;
            set
            {
                _colorTheme = value;
                OnPropertyChanged("ColorTheme");
            }
        }
        public MainViewModel()
        {
            SetupTimer();
            XsltParameters = new ObservableCollection<XsltParameter>();
            XsltParameters.CollectionChanged += (a, b) => RunTransform();
            Document = new Document();
            XmlToTransformDocument.TextChanged += (a, b) => RunTransform();
        }

        public List<XsltProcessingMode> XsltProcessingModes => Enum.GetValues(typeof(XsltProcessingMode)).Cast<XsltProcessingMode>().ToList();

        public XsltProcessingMode XsltProcessingMode
        {
            get => _xsltProcessingMode;
            set
            {
                _xsltProcessingMode = value;
                OnPropertyChanged("XsltProcessingMode");
                RunTransform();
            }
        }

        public string WindowTitle => Document == null ? ProgramName : $"{Document.Display} - {ProgramName}";

        public Document Document
        {
            get => _document;
            private set
            {
                _document = value;
                if (_document != null)
                {
                    _document.TextDocument.TextChanged += TextDocument_TextChanged;
                }

                OnPropertyChanged("Document");
                OnPropertyChanged("WindowTitle");
            }
        }

        private void TextDocument_TextChanged(object sender, EventArgs e)
        {
            RunTransform();
            OnPropertyChanged("WindowTitle");
        }

        public TextDocument XmlToTransformDocument { get; } = new TextDocument();

        public TextDocument ResultingXmlDocument { get; } = new TextDocument();

        public TextDocument ErrorsDocument { get; } = new TextDocument();

        public bool ErrorsExist => ErrorsDocument.Text.Length > 0;

        protected void TransformFinished()
        {
            var evt = OnTransformFinished;
            evt?.Invoke(this, EventArgs.Empty);
        }

        public ObservableCollection<XsltParameter> XsltParameters { get; }

        internal void OpenFile(string fileName)
        {
            Document = new Document(fileName);
            var paramNames = ExtractParamsFromXslt(Document.TextDocument.Text);
            XsltParameters.Clear();
            paramNames.ToList().ForEach((o) => XsltParameters.Add(new XsltParameter { Name = o }));
        }

        internal void New()
        {
            if (Document != null && Document.IsModified)
            {
                var answer = MessageBox.Show("You have unsaved changes in current document. Discard?", "Warning", MessageBoxButton.OKCancel);
                if (answer != MessageBoxResult.OK) return;
            }

            Document = new Document();
        }

        internal void Save()
        {
            if (Document == null)
            {
                Notifier.ShowWarning("No open file. This should not have happened :( Apologies.");
                return;
            }

            if (Document.IsNew)
            {
                var ofd = new SaveFileDialog
                {
                    Filter = "XSLT|*.xslt|All files|*.*",
                    RestoreDirectory = true,
                    Title = "Save new file as..."
                };
                if (ofd.ShowDialog() == true)
                {
                    Document.FilePath = ofd.FileName;
                }
            }

            try
            {
                if (Document.Save())
                    Notifier.ShowSuccess("Saved! ☃");
            }
            catch (Exception ex)
            {
                Notifier.ShowError(ex.ToString());
            }
        }
        public List<ColorTheme> ColorThemes { get { return ColorTheme.ColorThemes; } }

        private static IEnumerable<string> ExtractParamsFromXslt(string xslt)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xslt);
                XmlNode root = doc.DocumentElement;

                var nodes = root?.SelectNodes("//*[local-name()='param']/@name");
                var result = new List<string>();
                if (nodes == null) return result;
                foreach (var node in nodes)
                {
                    result.Add(((XmlAttribute)node).Value);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return new string[0];
            }
        }

        private Timer _runTransformTimer;

        private void SetupTimer()
        {
            _runTransformTimer = new Timer(200) { Enabled = false, AutoReset = false };
            _runTransformTimer.Elapsed += (sender, args) => Application.Current.Dispatcher.Invoke(RunTransformImpl);
        }

        public void RunTransform()
        {
            _runTransformTimer.Stop();
            _runTransformTimer.Start();
        }

        public bool RunTransformImpl()
        {
            if (XmlToTransformDocument == null || string.IsNullOrWhiteSpace(Document?.TextDocument?.Text))
                return false;

            var xml = XmlToTransformDocument.Text;
            var xslt = Document.TextDocument.Text;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    string result = null;
                    switch (XsltProcessingMode)
                    {
                        case XsltProcessingMode.SAXON:
                            result = SaxonTransformer.XsltTransformSaxon(xml, xslt, XsltParameters.Where(o => o?.Name != null).ToArray());
                            break;
                        case XsltProcessingMode.DOTNET:
                            result = XsltTransformDotNet(xml, xslt, XsltParameters.Where(o => o?.Name != null).ToArray());
                            break;
                        case XsltProcessingMode.JUST:
                            result = JsonTransformUsingJustNet(xml, xslt);
                            break;
                        default:
                            MessageBox.Show("Unknown transform method: " + XsltProcessingMode);
                            break;
                    }

                    var validation = Validate(result);
                    if (validation != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ResultingXmlDocument.Text = result;
                            ErrorsDocument.Text = validation;
                        }));
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ResultingXmlDocument.Text = result;
                            ErrorsDocument.Text = string.Empty;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ErrorsDocument.Text = ex.InnerException?.ToString() ?? ex.Message;
                    }));
                }
                finally
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        OnPropertyChanged("ErrorsExist");
                        TransformFinished();
                    }));
                }
            });

            return true;
        }

        private string Validate(string xml)
        {
            if (string.IsNullOrWhiteSpace(ValidationSchemaFile)) return null;
            if (string.IsNullOrWhiteSpace(xml)) return null;
            var schemas = new XmlSchemaSet();
            var schema = XmlSchema.Read(new XmlTextReader(ValidationSchemaFile), null);
            schemas.Add(schema);

            var doc = XDocument.Parse(xml);

            string message = null;
            doc.Validate(schemas, (o, e) =>
            {
                message = e.Message;
            });

            return message;
        }

        private string _validationSchemaFile;
        private ColorTheme _colorTheme = new ColorTheme();

        public string ValidationSchemaFile
        {
            get => _validationSchemaFile;
            set
            {
                _validationSchemaFile = value;
                OnPropertyChanged("ValidationSchemaFile");
            }
        }

        public static string XsltTransformDotNet(string xmlString, string xslt, XsltParameter[] xsltParameters)
        {
            using (var xmlDocumenOut = new StringWriter())
            using (StringReader xmlReader = new StringReader(xmlString), xsltReader = new StringReader(xslt))
            using (XmlReader xmlDocument = XmlReader.Create(xmlReader), xsltDocument = XmlReader.Create(xsltReader))
            {
                var xsltSettings = new XsltSettings(true, true);
                var myXslTransform = new XslCompiledTransform();
                myXslTransform.Load(xsltDocument, xsltSettings, new XmlUrlResolver());
                var argsList = new XsltArgumentList();
                xsltParameters?.ToList().ForEach(x => argsList.AddParam(x.Name, "", x.Value));
                using (var xmlTextWriter = XmlWriter.Create(xmlDocumenOut, myXslTransform.OutputSettings))
                {
                    myXslTransform.Transform(xmlDocument, argsList, xmlTextWriter);
                    return xmlDocumenOut.ToString().Replace("\n", Environment.NewLine).Trim('\uFEFF');
                }
            }
        }

        private static string JsonTransformUsingJustNet(string json, string transformer)
        {
            return MainWindow.PrettyJson(JsonTransformer.Transform(transformer, json));
        }
    }
}
