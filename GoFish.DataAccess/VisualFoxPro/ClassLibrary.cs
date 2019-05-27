using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GoFish.DataAccess.VisualFoxPro
{
    public class ClassLibrary
    {
        public List<Class> Classes { get; } = new List<Class>();
        public string Name { get; set; }

        public static ClassLibrary FromRows(string name, IEnumerable<object[]> rows)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Must not be empty.", nameof(name));
            }

            var lib = new ClassLibrary
            {
                Name = name
            };
            foreach (var row in rows)
            {
                var cl = Class.FromRow(row);
                lib.Classes.Add(cl);
            }

            return lib;
        }

        public static ClassLibrary FromPRG(string name, Stream stream, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Must not be empty.", nameof(name));
            }

            var lib = new ClassLibrary
            {
                Name = name
            };

            lib.ParsePRG(name, stream, encoding);

            return lib;
        }

        static Regex rxDefineClass = new Regex(@"DEFINE\s+CLASS\s+(\w+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static Regex rxDefine2 = new Regex("DEFINE", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void ParsePRG(string name, Stream stream, Encoding encoding)
        {
            var classBuilder = new StringBuilder();
            var className = "Main";
            // FASTPATH
            var mainClass = new Class
            {
                Name = "",
                BaseClass = "",
            };
            using (var fs = new StreamReader(stream, encoding))
            {
                mainClass.methods.Add(new Method
                {
                    Name = "",
                    Body = fs.ReadToEnd()
                });
            }
            Classes.Add(mainClass);


            return; // FASTPATH

            using (var fs = new StreamReader(stream, encoding))
            {
                while (fs.ReadLine() is string line)
                {
                    if (line.Contains("DEFINE CLASS", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = rxDefineClass.Match(line);
                        if (m.Success)
                        {
                            var cl = Class.FromPRG(className, classBuilder.ToString());
                            Classes.Add(cl);
                            classBuilder.Clear();
                            className = m.Groups[1].Value;
                        }
                    }
                    else if (line.Contains("ENDDEF", StringComparison.OrdinalIgnoreCase))
                    {
                        var cl = Class.FromPRG(className, classBuilder.ToString());
                        Classes.Add(cl);
                        classBuilder.Clear();
                    }
                    else
                    {
                        classBuilder.AppendLine(line);
                    }
                }
            }
            if (classBuilder.Length != 0)
            {
                var cl = Class.FromPRG(className, classBuilder.ToString());
                Classes.Add(cl);
                classBuilder.Clear();
            }
        }

        enum Tokens
        {
            None, BeginClass, EndClass, BeginMethod, EndMethod, Text
        }
    }
}
