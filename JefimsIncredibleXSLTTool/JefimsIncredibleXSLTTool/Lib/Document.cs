using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace JefimsIncredibleXsltTool.Lib
{
    public class Document : Observable
    {
        private string _filePath;
        private string _originalContents;
        private TextDocument _textDocument;

        public Document()
        {
            IsNew = true;
            var contents = string.Empty;
            _originalContents = string.Empty;
            TextDocument = new TextDocument(new StringTextSource(contents));
        }

        private string FormatXPath(string input)
        {
            string result;

            using (StringReader sr = new StringReader(input))
            {
                using (XmlReader xr = XmlReader.Create(sr))
                {
                    using (StringWriter sw = new StringWriter())
                    {

                        //xsltCompiled.Transform(xr, null, sw);

                        result = sw.ToString();
                    }
                }
            }

            return result;
        }

        public Document(string filePath)
        {
            IsNew = false;
            FilePath = filePath;
            var contents = File.ReadAllText(FilePath);
            _originalContents = contents;
            TextDocument = new TextDocument(new StringTextSource(contents));
            TextDocument.Changed += TextDocument_Changed;
        }

        private void TextDocument_Changed(object sender, DocumentChangeEventArgs e)
        {
            OnPropertyChanged("Display");
            OnPropertyChanged("IsModified");
        }

        public bool IsNew { get; private set; }

        public bool IsModified => TextDocument != null && TextDocument.Text != _originalContents;

        public TextDocument TextDocument
        {
            get => _textDocument;
            private set
            {
                _textDocument = value;
                OnPropertyChanged("TextDocument");
            }
        }

        public string Display
        {
            get
            {
                var result = IsNew ? "Unsaved document" : Path.GetFileName(FilePath);
                if (IsModified) result += " *";
                return result;
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                IsNew = false;
                OnPropertyChanged("FilePath");
                OnPropertyChanged("Display");
            }
        }

        internal bool Save()
        {
            if (FilePath == null)
            {
                return false;
            }
            File.WriteAllText(FilePath, TextDocument.Text);
            IsNew = false;
            _originalContents = TextDocument.Text;
            OnPropertyChanged("IsModified");
            OnPropertyChanged("Display");
            return true;
        }
    }
}
