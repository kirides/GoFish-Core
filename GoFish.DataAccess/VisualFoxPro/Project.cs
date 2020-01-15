using System;
using System.Collections.Generic;
using System.Text;
using static GoFish.DataAccess.VisualFoxPro.Constants;

namespace GoFish.DataAccess.VisualFoxPro
{

    public class Project
    {
        public string FullName { get; set; }
        public string HomeDir { get; set; }
        public ProjectHook ProjectHook { get; set; }

        public List<string> Databases { get; set; } = new List<string>();
        public List<string> Forms { get; set; } = new List<string>();
        public List<string> Classes { get; set; } = new List<string>();
        public List<string> Programs { get; set; } = new List<string>();
        public List<ProjectEntry> Entries { get; set; }

        public static Project FromRows(IEnumerable<object[]> rows)
        {
            var project = new Project();
            foreach (var row in rows)
            {
                switch (row[PJX.TYPE])
                {
                    case "H":
                        project.FillProjectHeader(row);
                        continue;
                    case "P": // PRG
                        project.Programs.Add(ReadNTString(row[PJX.NAME] as string));
                        break;
                    case "K": // FORM
                        project.Forms.Add(ReadNTString(row[PJX.NAME] as string));
                        break;
                    case "V": // CLASSLIB
                        project.Classes.Add(ReadNTString(row[PJX.NAME] as string));
                        break;
                    case "d": // DATABASE
                        project.Databases.Add(ReadNTString(row[PJX.NAME] as string));
                        break;
                    case "W": // PROJECT HOOK
                        project.ProjectHook = new ProjectHook
                        {
                            Class = ReadNTString(row[PJX.RESERVED1] as string),
                            Library = ReadNTString(row[PJX.NAME] as string),
                        };
                        break;
                }
            }

            return project;
        }

        private static string ReadNTString(string value)
        {
            return value.Substring(0, value.IndexOf('\0'));
        }

        private void FillProjectHeader(object[] row)
        {
            FullName = ReadNTString(row[PJX.NAME] as string);
            HomeDir = ReadNTString(row[PJX.HOMEDIR] as string);
        }
    }
}
