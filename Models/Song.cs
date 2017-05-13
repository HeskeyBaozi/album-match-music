using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Album.Models {
    class Song {
        public int Id { get; set; }
        public String Title { get; set; }
        public String Artist { get; set; }
        public String Album { get; set; }
        public StorageFile SongFile { get; set; }
        public Boolean Selected { get; set; }
        public BitmapImage AlbumCover { get; set; }
        public Boolean Used { get; set; }
    }
}
