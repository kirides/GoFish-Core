namespace GoFish.WpfUI
{
    public class SearchModel
    {
        public string Library { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }
        public int Line { get; set; }
        public string Content { get; set; }
        public string LineContent { get; set; }
        public string Filepath { get; set; }
    }
}