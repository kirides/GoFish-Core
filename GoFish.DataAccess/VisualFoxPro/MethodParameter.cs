namespace GoFish.DataAccess.VisualFoxPro;

public class MethodParameter
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Library { get; set; } = "";

    public override string ToString()
    {
        return $"{Name}{(!string.IsNullOrEmpty(Type) ? " AS " + Type : "")}";
    }
}
