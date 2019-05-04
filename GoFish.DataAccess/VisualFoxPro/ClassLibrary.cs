using System;
using System.Collections.Generic;

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
    }
}
