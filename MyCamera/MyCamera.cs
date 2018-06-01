using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace MyCamera
{
    class MyCamera
    {
        static void Main(string[] args)
        {
            string msg;
            string msgCaption = "Security Caemra Processor";
            ImportImages ic = new ImportImages();
            ic.ImportFromCamera();
            if (Directory.GetFiles(Properties.Settings.Default.LocalRawImagesFolder).GetLength(0) > 0)
            //if (ic.FilesImported > 0)
            {
                if (!String.IsNullOrEmpty(Properties.Settings.Default.ConverterApplicationFilename))
                    Process.Start(Properties.Settings.Default.ConverterApplicationFilename);
                msg = String.Format("1. Open 'H264 to AVI Converter'\n2. Use {0} as both 'Source Path' and 'Saving Path'.\n3. Select all, then convert.\n4.Close the converter app, then click 'OK'.", Properties.Settings.Default.LocalRawImagesFolder);
                if (MessageBox.Show(msg, msgCaption, MessageBoxButtons.OKCancel) == DialogResult.OK)
                    ic.SaveConvertedImages();
            }
            msg = String.Format("Files Downloaded: {0}\nConverted Files Saved: {1}\n\n\nFiles saved in:\n{2}", ic.FilesImported, ic.FilesSaved, Properties.Settings.Default.LocalFinalImagesFolder);
            MessageBox.Show(msg, msgCaption);
        }
    }
}
