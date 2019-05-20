using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GoFish.DataAccess.VisualFoxPro
{
    public class Class
    {
        private static readonly Pool<StringBuilder> stringBuilderPool = new Pool<StringBuilder>(1, () => new StringBuilder(8192));

        public static Class FromRow(object[] row)
        {
            var form = new Class
            {
                Name = (string)row[Constants.VCX.NAME],
                BaseClass = (string)row[Constants.VCX.CLASS]
            };
            using (var propertyBody = new StringReader((string)row[Constants.VCX.PROPERTIES]))
            {
                form.ParseProperties(propertyBody);
            }

            using (var methodBodies = new StringReader((string)row[Constants.VCX.BODY]))
            {
                form.ParseMethods(methodBodies);
            }

            return form;
        }

        private readonly List<Method> methods = new List<Method>();
        public IReadOnlyList<Method> Methods => methods;

        private readonly List<string> properties = new List<string>();
        public IReadOnlyList<string> Properties => properties.AsReadOnly();

        public string Name { get; set; } = "";
        public string BaseClass { get; set; } = "";

        private void ParseProperties(TextReader rdr)
        {
            while (rdr.ReadLine() is string line)
            {
                properties.Add(line);
            }
        }

        private void ParseMethods(TextReader rdr)
        {
            while (rdr.ReadLine() is string l)
            {
                if (l.StartsWith("PROC", StringComparison.OrdinalIgnoreCase))
                {
                    var method = ReadMethod(MethodType.Procedure, l, rdr, "ENDPR");
                    methods.Add(method);
                }
                else if (l.StartsWith("FUNC", StringComparison.OrdinalIgnoreCase))
                {
                    var method = ReadMethod(MethodType.Function, l, rdr, "ENDFU");
                    methods.Add(method);
                }
            }
        }

        private Method ReadMethod(MethodType type, string line, TextReader rdr, string terminator)
        {
            var method = new Method
            {
                Type = type,
                Name = line.Substring(line.IndexOf(' ') + 1),
            };
            var bodyBuilder = stringBuilderPool.Rent();

            while (rdr.ReadLine() is string bodyLine && !bodyLine.StartsWith(terminator, StringComparison.OrdinalIgnoreCase))
            {
                var trimmedLine = bodyLine.TrimStart();
                if (trimmedLine.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("LPARAM", StringComparison.OrdinalIgnoreCase))
                {
                    method.Parameters.AddRange(ReadParameters(rdr, trimmedLine.Substring(trimmedLine.IndexOf(' ') + 1)));
                }
                bodyBuilder.AppendLine(bodyLine);
            }

            method.Body = bodyBuilder.ToString();
            bodyBuilder.Clear();
            stringBuilderPool.Return(bodyBuilder);
            return method;
        }

        private IEnumerable<MethodParameter> ReadParameters(TextReader rdr, string trimmedLine)
        {
            bool lineOverlap;
            do
            {
                lineOverlap = false;
                foreach (var p in trimmedLine.Split(',', ';'))
                {
                    var pTrimmed = p.Trim();
                    var par = new MethodParameter { Name = pTrimmed };

                    // Read the parameter type info if it exists
                    if (pTrimmed.Contains(' ') || pTrimmed.Contains('\t'))
                    {
                        var itms = pTrimmed.Split(new char[] { '\t', ' ' }, 5, StringSplitOptions.RemoveEmptyEntries);
                        if (itms.Length > 4 && itms[3].Equals("OF", StringComparison.OrdinalIgnoreCase))
                        {
                            par.Library = itms[4];
                        }
                        if (itms.Length > 2 && itms[1].Equals("AS", StringComparison.OrdinalIgnoreCase))
                        {
                            par.Type = itms[2];
                        }
                        par.Name = itms[0];
                    }
                    yield return par;
                }
                if (trimmedLine.Contains(';'))
                {
                    lineOverlap = (trimmedLine = rdr.ReadLine()?.TrimStart()) != null;
                }
            } while (lineOverlap);
        }

        public override string ToString()
        {
            return $"{Name} ({BaseClass})";
        }
    }
}
