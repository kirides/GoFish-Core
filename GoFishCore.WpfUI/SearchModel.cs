using System.IO;

namespace GoFishCore.WpfUI
{
    public class SearchModel
    {
        public string Library { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }
        public int Line { get; set; }
        private string content;
        public string Content { get => content ?? (Filepath != null ? File.ReadAllText(Filepath) : ""); set => content = value; }
        public string LineContent { get; set; }
        public string Filepath { get; set; }
    }
}
