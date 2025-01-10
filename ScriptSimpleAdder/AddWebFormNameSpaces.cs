using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ude;

namespace ConsoleApp1
{
    public class AddWebFormNameSpaces
    {

        public static string EnsureTrailingSlash(string input)
        {
            if (!input.EndsWith("/"))
            {
                input += "/";
            }
            return input;
        }

        public static void AddNameSpacesToFiles(string projectPath)
        {
            projectPath = EnsureTrailingSlash(projectPath);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            foreach (var file in Directory.GetFiles(projectPath, "*.aspx",
                         SearchOption.AllDirectories))
            {
                var className = Path.GetFileNameWithoutExtension(file);
               
                className = JsonNamingPolicy.SnakeCaseUpper.ConvertName(className.ToLower() == "default" ? "_Default" : className);
                className = className.ToLower() == "new" ? "_New" : className;
                var namespaceName = $"BarsWeb";
                var dir = Path.GetDirectoryName(file) ?? string.Empty;
                var prefLen = projectPath.Length;
                if (dir.Length > prefLen)
                    namespaceName = JsonNamingPolicy.CamelCase.ConvertName($"{namespaceName}.{dir.Substring(prefLen).Replace('\\', '/').Replace('/', '.').Replace("-", "")}");

                Encoding encoding = GetFileEncoding(file);

                StringBuilder sb = new StringBuilder();
                using (var aspxFile = new FileStream(file, FileMode.Open))
                {
                    using (var streamReader = new StreamReader(aspxFile, encoding))
                    {
                        bool anotationBegin = false;
                        string anotationLines = "";
                        int row = 0;

                        while (streamReader.ReadLine() is { } line)
                        {
                            if (line.Contains("<%@", StringComparison.OrdinalIgnoreCase))
                                anotationBegin = true;

                            if(anotationBegin) 
                                row++;

                            if (anotationBegin && !(line.Contains("<%@", StringComparison.OrdinalIgnoreCase) && row > 1))
                            {
                                if (String.IsNullOrEmpty(anotationLines))
                                    anotationLines = line;
                                else
                                    anotationLines = $"{anotationLines} {line}";                                
                            }                                

                            if (line.Contains("%>", StringComparison.OrdinalIgnoreCase) || (line.Contains("<%@", StringComparison.OrdinalIgnoreCase) && row > 1))
                            {
                                anotationBegin = false;
                                if (line.Contains("<%@", StringComparison.OrdinalIgnoreCase) && row > 1)
                                    anotationLines = $"{anotationLines} %>";
                            }
                                

                            if (anotationLines.Contains("Inherits", StringComparison.OrdinalIgnoreCase) &&
                                anotationLines.Contains("Language", StringComparison.OrdinalIgnoreCase) && 
                                !anotationBegin &&
                                !String.IsNullOrEmpty(anotationLines))
                            {
                                var keyPaser = anotationLines.Replace(" =", "=").Replace("= ", "=").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                anotationLines = "";
                                List<string> tmpList = new List<string>();

                                for (int i = 0; i < keyPaser.Length; i++)
                                {
                                    if (keyPaser[i].Contains("Inherits", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var val = keyPaser[i].Split('=', StringSplitOptions.RemoveEmptyEntries);
                                        keyPaser[i] = $"{val[0]}=\"{namespaceName}.{className}\"";
                                    }

                                    if(keyPaser[i].Contains("Title", StringComparison.OrdinalIgnoreCase))
                                    {

                                    }
                                }

                                if (!anotationBegin)
                                {
                                    string tmp = string.Join(' ', keyPaser);
                                    //if (tmp.Length > 150)
                                    //{
                                    //    tmp = "";
                                    //    int outRow = 0;
                                    //    for (int i = 0; i < keyPaser.Length; i++)
                                    //    {
                                    //        if (String.IsNullOrEmpty(tmp))
                                    //            tmp = keyPaser[i];
                                    //        else
                                    //            tmp = $"{tmp} {keyPaser[i]}";
                                    //        if (tmp.Length - outRow * 100 >= 100)
                                    //        {
                                    //            tmp = tmp + Environment.NewLine + "   ";
                                    //            outRow++;
                                    //        }
                                                
                                    //    }
                                    //}
                                    
                                    sb.AppendLine(tmp);
                                }
                                    

                                if(line.Contains("<%@", StringComparison.OrdinalIgnoreCase) && row > 1)
                                    sb.AppendLine(line);
                            }
                            else
                            {
                                if (!anotationBegin)
                                {
                                    if (!String.IsNullOrEmpty(anotationLines))
                                    {
                                        if (row == 1)
                                            sb.AppendLine(anotationLines);
                                        anotationLines = "";
                                    }
                                        
                                    sb.AppendLine(line);
                                }
                                   
                            }
                        }
                    }
                }

                File.WriteAllText(file, sb.ToString(), encoding);

                foreach (var ext in new List<String>() { ".cs", ".designer.cs" })
                {
                    try
                    {
                        StringBuilder sb1 = new StringBuilder();

                        using (var csFile = new FileStream($"{file}{ext}", FileMode.Open))
                        using (var streamReader = new StreamReader(csFile, encoding))
                        {
                            bool isNamespace = false;
                            bool clasReplaced = false;
                            bool isNsSet = false;
                            string oldClassName = "";

                            while (streamReader.ReadLine() is { } line)
                            {
                                if (line.Contains("enum ", StringComparison.OrdinalIgnoreCase) && !clasReplaced)
                                {
                                    if (!isNamespace)
                                    {
                                        sb1.AppendLine($"namespace {namespaceName}");
                                        sb1.AppendLine("{");
                                        sb1.AppendLine("");
                                        isNamespace = true;
                                        isNsSet = true;
                                    }
                                }

                                if (line.Contains(" class ", StringComparison.OrdinalIgnoreCase) && !clasReplaced)
                                {
                                    if (!isNamespace)
                                    {
                                        sb1.AppendLine($"namespace {namespaceName}");
                                        sb1.AppendLine("{");
                                        sb1.AppendLine("");
                                        isNamespace = true;
                                        isNsSet = true;
                                    }

                                    var classPosition = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                    for (int i = 0; i < classPosition.Length; i++)
                                    {
                                        if (classPosition[i].Contains("class", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (classPosition[i + 1].Contains(":", StringComparison.OrdinalIgnoreCase))
                                            {
                                                oldClassName = classPosition[i + 1];
                                                classPosition[i + 1] = className + " :";
                                            }
                                            else
                                            {
                                                oldClassName = classPosition[i + 1];
                                                classPosition[i + 1] = className;
                                            }
                                        }
                                    }

                                    sb1.AppendLine($"    {string.Join(' ', classPosition)}");
                                    clasReplaced = true;
                                }
                                else
                                {
                                    if (!line.Contains("namespace", StringComparison.OrdinalIgnoreCase) || isNamespace)
                                        if (line.Contains(oldClassName, StringComparison.OrdinalIgnoreCase) && line.Contains("public ", StringComparison.OrdinalIgnoreCase) && !line.Contains("class", StringComparison.OrdinalIgnoreCase) && clasReplaced)
                                        {
                                            sb1.AppendLine(line.Replace(oldClassName, className));
                                        }
                                        else
                                        {
                                            sb1.AppendLine(isNsSet ? $"    {line}" : line);
                                        }
                                }

                                if (line.Contains("namespace", StringComparison.OrdinalIgnoreCase) && !isNamespace)
                                {
                                    isNamespace = true;
                                    if (line.Contains(namespaceName, StringComparison.OrdinalIgnoreCase))
                                        sb1.AppendLine(line);
                                    else
                                        sb1.AppendLine($"namespace {namespaceName}");
                                }
                            }

                            if (isNsSet) sb1.AppendLine("}");
                        }

                        var regex = new Regex("\\?{3,}", RegexOptions.Compiled);

                        if (regex.IsMatch(sb1.ToString()))
                        {
                            Console.WriteLine($"{file}{ext}");
                        }

                        using (var fs = new FileStream($"{file}{ext}", FileMode.OpenOrCreate))
                        using (var sw = new StreamWriter(fs, encoding))
                        {
                            sw.Write(sb1.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private static Encoding GetFileEncoding(string srcFile)
        {
            var detector = new CharsetDetector();
            using (var stream = File.OpenRead(srcFile))
            {
                detector.Feed(stream);
                detector.DataEnd();
            }

            try
            {
                return detector.Charset != null ? Encoding.GetEncoding(detector.Charset) : Encoding.UTF8;
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8;
            }
        }
    }
}
