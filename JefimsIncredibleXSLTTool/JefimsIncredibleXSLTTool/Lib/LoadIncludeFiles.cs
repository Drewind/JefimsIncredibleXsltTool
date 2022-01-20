using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JefimsIncredibleXsltTool.Lib
{
    public class LoadIncludeFiles
    {
        private Document file;

        public LoadIncludeFiles(String fileName)
        {
            this.file = new Document(fileName);
        }

        public static Boolean LoadFile()
        {
            try
            {
                // load file
            } catch (IOException ex)
            {
                // file not found
                return false;
            }
            return true;
        }
    }
}
