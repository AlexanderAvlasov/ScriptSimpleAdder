using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Linq;
using AngleSharp;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using Microsoft.WindowsAPICodePack.Dialogs;
using AngleSharp.Dom;
using ConsoleApp1;

namespace ScriptSimpleAdder
{
    public static class OutputExt
    {
        public static void AppendLine(this TextBox tb, string line)
        {
            tb.AppendText($"{line}\r\n");
        }

        public static string ReplaceDots(this string str) {
            return str.Replace(".", "\\.");
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        string projectPath;
        List<string> skipFiles;
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            output.Clear();
            projectPath = ProjectPath.Text;
            EnumerationOptions opt = new EnumerationOptions() { RecurseSubdirectories=true};
            listScripts = new List<string> { };
            listScripts.AddRange(Regex.Split(scriptsToAdd.Text, "\\r\\n|\\n").Select(line => line.Trim()));
            skipFiles = new List<string>();
            skipFiles.AddRange(Regex.Split(SkipTextBox.Text, "\\r\\n|\\n").Select(line => line.Trim()));
            var arrFileExt =new string[]{ "*.cshtml", "*.aspx", "*.js",};
            var files = new List<string>();
            //Достаем по каждому типу файлы что пройдут регулярку.

            files = await GetFiltredFilesAsync("*.cshtml;*.aspx;*.js", searchExp.Text, true);
                
            output.AppendLine($"Files was found {files.Count}");
            //Фільтруємо що пропускаєм
            files = files.Where(file => {
                return !skipFiles.Contains(file);
            }).ToList();
            output.AppendLine($"Files after filter {files.Count}");
            List<string> filesThatNeedAdded = new List<string>();
            //Found all match
            foreach (var file in files)
            {
                FileInfo fileContainedSearchedText = new FileInfo(file);
                switch (fileContainedSearchedText.Extension.ToLower())
                {
                    case ".js":
                        //[\\\/]Common[\\\/]WebGrid[\\\/]Grid2005.js
                        string fileRelativeToProject = fileContainedSearchedText.FullName.Replace(ProjectPath.Text,"").Replace("\\",@"[\\\/]").Replace(".", "\\.");
                        //if (fileContainedSearchedText.FullName == @"F:\nbu\NBU_CHROME_REDESIGN\abs_cnbu\web\barsroot\viewaccounts\Scripts\Common.js") {
                        //    var found = true;
                        //}

                        var toChange = await GetFiltredFilesAsync("*.cshtml;*.aspx", fileRelativeToProject);
                        var toChange2 = new List<string>();
                        //~ get from barsroot
                        if (fileRelativeToProject.Contains("barsroot"))
                        {
                            var jsBarsrootPath = Regex.Replace(fileRelativeToProject, @"^.*barsroot[\\\/]", "~");
                            toChange2 = await GetFiltredFilesAsync("*.cshtml;*.aspx", jsBarsrootPath);
                        }
                        if (toChange.Count > 0)
                        {
                            output.AppendText($"To change {fileContainedSearchedText.FullName} {toChange.FirstOrDefault()}\r\n");
                            filesThatNeedAdded.AddRange(toChange);
                        }
                        if (toChange2.Count > 0)
                        {
                            filesThatNeedAdded.AddRange(toChange2);
                            output.AppendText($"To change2 {fileContainedSearchedText.FullName} {toChange2.FirstOrDefault()}\r\n");
                        }


                        //Найти cshtml с js которые прописаны с относительным путем
                        var toChange3 = await GetFiltredFilesAsync("*.cshtml;*.aspx", $"src\\s*=\\s*[\"'](([^\\\\\\/~].*{fileContainedSearchedText.Name.ReplaceDots()}.*['\"])|({fileContainedSearchedText.Name.ReplaceDots()}.*['\"]))");
                        var result = toChange3.FindAll(htmlPath => {
                            HtmlParser parser = new HtmlParser();
                            var htmlStr = File.ReadAllText(htmlPath);
                            //из файоа вырезаем src
                            var match = Regex.Match(htmlStr, $"src\\s*=\\s*[\"'][^\\\\\\/~].*{fileContainedSearchedText.Name.ReplaceDots()}.*['\"]");
                            var ralativePath = Regex.Match(Regex.Replace(match.Value, "src\\s*=\\s*[\"']",""),"[^\\?\"']*").Value;
                            var currentJsPath = Path.GetFullPath(Path.Combine(new FileInfo(htmlPath).Directory.ToString(), ralativePath));
                            return currentJsPath == fileContainedSearchedText.FullName;
                        });
                        if (toChange3.Count > 0) {
                            filesThatNeedAdded.AddRange(toChange3); 
                            output.AppendText($"To toChange3 {fileContainedSearchedText.FullName} {toChange3.FirstOrDefault()}\r\n");
                        }
                        
                        //сравниваем пути изначального js и что получили

                        break;
                        default: 
                            filesThatNeedAdded.Add(fileContainedSearchedText.FullName);
                        break;

                }

                
            }
            foreach (var file2 in filesThatNeedAdded)
            {
                AddScriptsToAspxOrHtml(file2);
            }
            output.AppendText("finish");
        }

        public Match FindScriptInText(string text, string src) {
            if ((src.Contains("jquery.js", StringComparison.OrdinalIgnoreCase)
                      || src.Contains("jquery.min.js", StringComparison.OrdinalIgnoreCase))) src = "(jquery.min.js|jquery.js|jquery-[\\d.]{1,8}(.min.js|.js))";
          return  Regex.Match(text, $"<script.*src\\s*=\\s*[\"'].*{src.ReplaceDots()}.*\\/script\\s*>[\\n|\\r\\n]*");
        }
        public Match FindCssInText(string text, string src) {
          return  Regex.Match(text, $"<link.*href\\s*=\\s*[\"'].*{src.ReplaceDots()}.*>[\\n|\\r\\n]*");
        }

        public void FindFullJsPath(string pathToHtml, string jsName) {
            using (FileStream fs = new FileStream(pathToHtml, FileMode.Open)) { 
                
            }
        }

        List<string> listScripts = new List<string>();

        public string getSrcFile(string line) {
            var match = Regex.Match(line, $"src\\s*=\\s*[\"'].*['\"]");
            return Regex.Match(Regex.Replace(match.Value, "src\\s*=\\s*[\"']", ""), "[^\\?\"']*").Value;
        }
        public string getLinkFile(string line) {
            var match = Regex.Match(line, $"href\\s*=\\s*[\"'].*['\"]");
            return Regex.Match(Regex.Replace(match.Value, "href\\s*=\\s*[\"']", ""), "[^\\?\"']*").Value;
        }


        public Encoding GetFileEncoding(string path) {
            var bom = new byte[4];
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }
            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return new UTF7Encoding(true);
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return new UTF8Encoding(true);
            if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0) return Encoding.UTF32; //UTF-32LE
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return new UTF32Encoding(true, true);  //UTF-32BE


            using (FileStream streamReader = new FileStream(path, FileMode.Open, FileAccess.Read)) {
                
                Ude.CharsetDetector detector = new Ude.CharsetDetector();
                detector.Feed(streamReader);
                detector.DataEnd();
                
                return Encoding.GetEncoding(detector.Charset);
                
            }

        }

        public void AddScriptsToAspxOrHtml(string file) {
            var fileEncodig = GetFileEncoding(file);
            var html = File.ReadAllText(file, fileEncodig);
            if (file.Contains("AcrInt.aspx")) {
                output.AppendLine(fileEncodig.ToString());
            }
            StringBuilder result = new StringBuilder(html);
            //Добавить логику также для layout и вставка в head если нет script
            int indexOfFirstScript = html.IndexOf("<script");
            //Ищем каждый скрипт что нужно добавить
            foreach (string script in listScripts) {
                bool isCss = false;
                var srcToAdd = getSrcFile(script);
                if (( srcToAdd.Contains("jquery.js", StringComparison.OrdinalIgnoreCase)
                      ||srcToAdd.Contains("jquery.min.js", StringComparison.OrdinalIgnoreCase))
                    && (new FileInfo(file)).Extension==".cshtml" ) {
                    continue;
                }
                if (String.IsNullOrEmpty(srcToAdd)) {
                    //Пробуем парсить css
                    srcToAdd=getLinkFile(script);
                    if(String.IsNullOrEmpty(srcToAdd)) continue;
                    isCss = true;
                } 
                FileInfo fi = new FileInfo(srcToAdd);
                //Смотрим есть ли самый первый в файле.
                var match= isCss?FindCssInText(result.ToString(), fi.Name) : FindScriptInText(result.ToString(), fi.Name);
                if (match.Success)
                {
                    indexOfFirstScript = match.Index+match.Length;
                }
                else {
                    if (indexOfFirstScript>0) {
                        result.Insert(indexOfFirstScript, script + "\r\n");
                        indexOfFirstScript += script.Length + 2;
                    }
                    else {
                        //Script на странице нет ищем head
                        match=Regex.Match(html,"<\\s*head.*>[\\n|\\r\\n]*");
                        if (!match.Success) {
                            output.AppendLine($"ERROR: no script or head in file {file}"); return; 
                        }
                        indexOfFirstScript = match.Index + match.Length;
                        result.Insert(indexOfFirstScript, script + "\r\n");
                    }
                }
            }
            using (StreamWriter sr = new StreamWriter(new FileStream(file, FileMode.Create, FileAccess.Write), fileEncodig))
            {
                sr.Write(result);
            }
            //File.WriteAllBytes(file, fileEncodig.GetBytes(result.ToString()));

        }

        public async Task<List<string>> GetFiltredFilesAsync(string fileExtRegex, string regExp, bool showCount=false) {
            var files= new List<string>();
            IEnumerable<string> extList = fileExtRegex.Split(';').Select(x => x.Trim());
            foreach (var fileExt in extList)
            {
                var foundedFies = (await GetFileFiltredByRegexAsync(fileExt, regExp));
                files.AddRange(foundedFies);
                if(showCount)output.AppendLine($"{fileExt} found {foundedFies.Count}");
            };
            return files;
        }

        public static async Task<Match> GetRegexpResultFromFileAsync(string file, string regexStr) {
            if (!File.Exists(file))
            {
                file = AddWebFormNameSpaces.FindFileIgnoreCase(file);
                if (!File.Exists(file)) return null;
            }

            using (StreamReader sr = new StreamReader(file))
            {
                string content = await sr.ReadToEndAsync();
                var regex = new Regex(regexStr, RegexOptions.IgnoreCase);
               
                return regex.Match(content);
            }
        }


        public async Task<List<string>> GetFileFiltredByRegexAsync(string fileExt, string regExp) {
            List<string> filesResult = new List<string>();
            var files = Directory.GetFiles(projectPath, fileExt, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                using (StreamReader sr = new StreamReader(file))
                {
                  string content= await sr.ReadToEndAsync();
                  var regex = new Regex(regExp, RegexOptions.IgnoreCase);
                  if(regex.Match(content).Success) filesResult.Add(file);
                }
            }
            return filesResult;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            output.Clear();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true, // Указываем, что выбираем папку
                Title = "Выберите папку"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ProjectPath_2.Text = dialog.FileName; // Путь к выбранной папке
                
            }
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            projectPath = ProjectPath_2.Text;
            var toChange = await GetFiltredFilesAsync("*.aspx", "Codebehind=[\"].*[\"]");
            //Пишем файлы что меняем
            AppendAllStrings(ProjectPath_2_Copy, toChange);

            foreach (var filePath in toChange)
            {
                var strFromFile = await GetRegexpResultFromFileAsync(filePath, "Codebehind=[\"].*[\"]");
                Regex pattern = new Regex(@"Codebehind=""([^""]+)""");
                var codeBehindFile  = Regex.Match(strFromFile.Value, @"""([^""]+)""", RegexOptions.IgnoreCase).Value;

                var fileEncodig = GetFileEncoding(filePath);
                var aspxPage = File.ReadAllText(filePath, fileEncodig);
                StringBuilder result = new StringBuilder(aspxPage);
                result.Insert(strFromFile.Index+strFromFile.Length, $" CodeFile=\"{codeBehindFile.Trim('"')}\" ");
                WriteToFile(result, filePath, fileEncodig);
            }
            
        }

        public void WriteToFile(StringBuilder result, string filePath, Encoding fileEncodig) {
           


            using (StreamWriter sr = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write), fileEncodig))
            {
                sr.Write(result);
            }
        }


        public void AppendAllStrings(TextBox tb, List<string> strList) { 
            foreach (var str in strList)
            {
                ProjectPath_2_Copy.AppendLine(str);
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            AddWebFormNameSpaces.AddNameSpacesToFiles(ProjectPath_2.Text);
        }
    }
}
