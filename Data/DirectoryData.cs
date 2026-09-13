namespace PlanetGame.Data
{
    public class DirectoryData : ISavable
    {
        public DirectoryData() { }

        public string BaseDirectory { get; set; }

        public string BaseAlbedo { get; set; }
        public string BaseHeightmap { get; set; }

        public string ThumbnailAlbedo { get; set; }
        public string ThumbnailHeightmap { get; set; }

        public string TileAlbedo { get; set; }
        public string TileHeightmap { get; set; }
        public string TileNormalMap { get; set; }

        public override string ToString()
        {
            return $"""
            BaseDirectory: {BaseDirectory}
            BaseAlbedo: {BaseAlbedo}
            BaseHeightmap: {BaseHeightmap}

            ThumbnailAlbedo: {ThumbnailAlbedo}
            ThumbnailHeightmap: {ThumbnailHeightmap}

            TilesAlbedo: {TileAlbedo}
            TilesHeightmap: {TileHeightmap}
            TilesNormalMap: {TileNormalMap}
            """;
        }
    }
}