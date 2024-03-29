﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GoFish.DataAccess.VisualFoxPro;

public class Class
{
    private static readonly Pool<StringBuilder> stringBuilderPool = new Pool<StringBuilder>(1, () => new StringBuilder(8192));

    public static Class FromRow(object[] row)
    {
        var form = new Class
        {
            Name = row[Constants.VCX.NAME] as string,
            BaseClass = row[Constants.VCX.CLASS] as string
        };

        
        if (row[Constants.VCX.PROPERTIES] is string properties && properties.Length > 0)
        {
            form.ParseProperties(properties.AsMemory());
        }

        if (row[Constants.VCX.BODY] is string body && body.Length > 0)
        {
            using var methodBodies = new StringReader(body);
            form.ParseMethods(methodBodies);
        }

        return form;
    }
    public static Class FromPRG(string name, string body)
    {
        var form = new Class
        {
            Name = name,
            BaseClass = "PRG"
        };
        using (var methodBodies = new StringReader(body))
        {
            form.ParseMethods(methodBodies);
        }

        return form;
    }
    internal readonly List<Method> methods = new List<Method>();
    public IReadOnlyList<Method> Methods => methods;

    internal readonly List<ReadOnlyMemory<char>> properties = new List<ReadOnlyMemory<char>>();
    public IReadOnlyList<ReadOnlyMemory<char>> Properties => properties.AsReadOnly();

    public string Name { get; set; } = "";
    public string BaseClass { get; set; } = "";

    private void ParseProperties(ReadOnlyMemory<char> body)
    {
        foreach (var line in body.EnumerateLines())
        {
            properties.Add(line);
        }
    }

    private void ParseMethods(TextReader rdr)
    {
        while (rdr.ReadLine() is string l)
        {
            var trimmed = l.TrimStart();
            if (trimmed.StartsWith("PROC", StringComparison.OrdinalIgnoreCase))
            {
                var method = ReadMethod(MethodType.Procedure, l, rdr);
                methods.Add(method);
            }
            else if (trimmed.StartsWith("FUNC", StringComparison.OrdinalIgnoreCase))
            {
                var method = ReadMethod(MethodType.Function, l, rdr);
                methods.Add(method);
            }
        }
    }

    private void ParseMethodsPRG(TextReader rdr)
    {
        var mainMethod = new Method { Name = "Main", Type = MethodType.Procedure };
        var mainMethodBodyBuilder = new StringBuilder();
        while (rdr.ReadLine() is string l)
        {
            if (l.StartsWith("PROC", StringComparison.OrdinalIgnoreCase))
            {
                var method = ReadMethod(MethodType.Procedure, l, rdr);
                methods.Add(method);
            }
            else if (l.StartsWith("FUNC", StringComparison.OrdinalIgnoreCase))
            {
                var method = ReadMethod(MethodType.Function, l, rdr);
                methods.Add(method);
            }
            else
            {
                mainMethodBodyBuilder.AppendLine(l);
            }
        }
        mainMethod.Body = mainMethodBodyBuilder.ToString();
        methods.Add(mainMethod);
    }

    private Method ReadMethod(MethodType type, string line, TextReader rdr)
    {
        var method = new Method
        {
            Type = type,
            Name = line.AsSpan(line.IndexOf(' ') + 1).ToString(),
        };
        var bodyBuilder = stringBuilderPool.Rent();

        while (rdr.ReadLine() is string bodyLine)
        {
            var trimmedLine = bodyLine.AsSpan().TrimStart();
            if (trimmedLine.StartsWith("ENDFU", StringComparison.OrdinalIgnoreCase)
                || trimmedLine.StartsWith("ENDPR", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (trimmedLine.StartsWith("PROC", StringComparison.OrdinalIgnoreCase))
            {
                methods.Add(ReadMethod(MethodType.Procedure, bodyLine, rdr));
                break;
            }
            else if (trimmedLine.StartsWith("FUNC", StringComparison.OrdinalIgnoreCase))
            {
                methods.Add(ReadMethod(MethodType.Function, bodyLine, rdr));
                break;
            }
            if (trimmedLine.StartsWith("PARAM", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("LPARAM", StringComparison.OrdinalIgnoreCase))
            {
                method.Parameters.AddRange(ReadParameters(rdr, trimmedLine.Slice(trimmedLine.IndexOf(' ') + 1).ToString()));
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
