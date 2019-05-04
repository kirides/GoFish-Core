using System.Collections.Generic;
using System.Linq;

namespace GoFish.DataAccess.VisualFoxPro
{
    public class Method
    {
        public MethodType Type { get; set; } = MethodType.Procedure;
        public string Name { get; set; } = "";
        public string Body { get; set; } = "";
        public List<MethodParameter> Parameters { get; } = new List<MethodParameter>();

        public override string ToString()
        {
            return $"{Name}({string.Join(", ", Parameters.Select(x=> $"{x.Name}{(!string.IsNullOrEmpty(x.Type) ? " AS " +x.Type : "")}"))})";
        }
    }
}
