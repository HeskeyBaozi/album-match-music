using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Album.Models;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace Album {
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page {
        private ObservableCollection<Song> Songs;
        private ObservableCollection<StorageFile> AllSongs;
        private Boolean _playingMusic = false;
        private int _round = 0;
        private int _totalScore = 0;

        public MainPage() {
            this.InitializeComponent();
            Songs = new ObservableCollection<Song>();
        }

        private async Task RetrieveFilesInFolders(
            ObservableCollection<StorageFile> list,
            StorageFolder parent) {

            foreach (var item in await parent.GetFilesAsync()) {
                if (item.FileType == ".mp3") {
                    list.Add(item);
                }
            }

            foreach (var item in await parent.GetFoldersAsync()) {
                await RetrieveFilesInFolders(list, item);
            }
        }

        private async Task<ObservableCollection<StorageFile>> SetupMusicList() {
            // Get access to Music Library.
            StorageFolder folder = KnownFolders.MusicLibrary;
            var allSongs = new ObservableCollection<StorageFile>();
            await RetrieveFilesInFolders(allSongs, folder);

            return allSongs;
        }

        private async Task PrepareNewGame() {
            Songs.Clear();

            // State management
            InstructionTextBlock.Text = "Get Ready :)";
            ResultTextBlock.Text = "";
            TitleTextBlock.Text = "";
            ArtistTextBlock.Text = "";
            AlbumTextBlock.Text = "";
            ResultImage.Source = null;
            _totalScore = 0;
            _round = 0;

            // Choose random songs from library.
            var randomSongs = await PickRandomSongs(AllSongs, 10);

            // Pluck off meta data from selected songs.
            await PopulateSongList(randomSongs);
            StartCooldown();
        }

        private async void Grid_Loaded(object sender, RoutedEventArgs e) {
            StartUpProgressRing.IsActive = true;

            AllSongs = await SetupMusicList();
            await PrepareNewGame();

            StartUpProgressRing.IsActive = false;

            StartCooldown();
        }

        private async Task<List<StorageFile>> PickRandomSongs(ObservableCollection<StorageFile> allSongs, int pickNumber) {

            Random random = new Random();
            int songCount = allSongs.Count;
            var randomSongs = new List<StorageFile>();

            while (randomSongs.Count < pickNumber) {
                var randomNumber = random.Next(songCount);
                var randomSong = allSongs[randomNumber];

                MusicProperties randomSongMusicProperties =
                                    await randomSong.Properties.GetMusicPropertiesAsync();

                bool isDuplicate = false;
                foreach (var song in randomSongs) {
                    MusicProperties songMusicProperties =
                        await song.Properties.GetMusicPropertiesAsync();

                    // Find random songs But:
                    // (1) Don't pick the same song twice.
                    // (2) Don't pick a song from an album that I've already picked.

                    if (String.IsNullOrEmpty(randomSongMusicProperties.Album)
                        || randomSongMusicProperties.Album == songMusicProperties.Album) {
                        isDuplicate = true;
                    }
                }
                if (!isDuplicate) {
                    randomSongs.Add(randomSong);
                }
            }
            return randomSongs;
        }

        private async Task PopulateSongList(List<StorageFile> files) {

            int id = 0;

            foreach (var file in files) {

                MusicProperties songProperties = await file.Properties.GetMusicPropertiesAsync();

                StorageItemThumbnail currentThumb = await file.GetThumbnailAsync(
                    ThumbnailMode.MusicView,
                    200,
                    ThumbnailOptions.UseCurrentScale);

                var albumCover = new BitmapImage();
                albumCover.SetSource(currentThumb);

                var song = new Song();
                song.Id = id;
                song.Title = songProperties.Title;
                song.Artist = songProperties.Artist;
                song.Album = songProperties.Album;
                song.Selected = false;
                song.Used = false;
                song.AlbumCover = albumCover;
                song.SongFile = file;

                Songs.Add(song);
                id++;
            }
        }

        private async void SongGridView_ItemClick(object sender, ItemClickEventArgs e) {

            // Ignore clicks when we are in cooldown
            if (!_playingMusic) {
                return;
            }

            CountDown.Pause();
            MyMediaElement.Stop();

            Song clickedSong = (Song)e.ClickedItem;
            Song correctSong = Songs.FirstOrDefault(p => p.Selected);

            // Evaluate the user's selection
            Uri uri = null;
            int addScore = 0;

            if(clickedSong.Selected) {
                uri = new Uri("ms-appx:///Assets/correct.png");
                addScore = (int)MyProgressBar.Value;
            } else {
                uri = new Uri("ms-appx:///Assets/incorrect.png");
                addScore = (-1) * (int)MyProgressBar.Value;
            }

            // Setting the picture.
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var fileStream = await file.OpenAsync(FileAccessMode.Read);
            await clickedSong.AlbumCover.SetSourceAsync(fileStream);

            _totalScore += addScore;
            _round++;
            ResultTextBlock.Text = String.Format("Score: {0}, Total Score after {1} Rounds: {2}", addScore, _round, _totalScore);
            DisplayCorrectSong(correctSong);

            clickedSong.Used = true;
            correctSong.Selected = false;
            correctSong.Used = true;

            if(_round >= 5) {
                InstructionTextBlock.Text = String.Format("Game Over... You scored: {0}", _totalScore);
                PlayAgainButton.Visibility = Visibility.Visible;
            } else {
                StartCooldown();
            }
        }

        private void DisplayCorrectSong(Song correctSong) {
            TitleTextBlock.Text = String.Format("{0}", correctSong.Title);
            ArtistTextBlock.Text = String.Format("{0}", correctSong.Artist);
            AlbumTextBlock.Text = String.Format("{0}", correctSong.Album);
            ResultImage.Source = correctSong.AlbumCover;
        }

        private async void PlayAgainButton_Click(object sender, RoutedEventArgs e) {
            PlayAgainButton.Visibility = Visibility.Collapsed;
            StartUpProgressRing.IsActive = true;
            await PrepareNewGame();
            StartUpProgressRing.IsActive = false;
        }

        private void StartCooldown() {
            _playingMusic = false;
            InstructionTextBlock.Text = String.Format("Get ready for round {0}...", _round + 1);
            ShortCountDown.Begin();
        }

        private void StartCountdown() {
            _playingMusic = true;
            InstructionTextBlock.Text = "Playing Music...♪";
            CountDown.Begin();
        }

        private Song PickSong() {
            Random random = new Random();
            var unusedSongs = Songs.Where(p => !p.Used);
            var randomNumber = random.Next(unusedSongs.Count());
            var randomSong = unusedSongs.ElementAt(randomNumber);
            randomSong.Selected = true;
            return randomSong;
        }

        private async void CountDown_Completed(object sender, object e) {
            if (!_playingMusic) {
                // Start playing music
                var song = PickSong();

                // Play the music
                MyMediaElement.SetSource(
                    await song.SongFile.OpenAsync(FileAccessMode.Read),
                    song.SongFile.ContentType);

                // Start countdown
                StartCountdown();
            } else {
                MyMediaElement.Stop();
                Song correctSong = Songs.FirstOrDefault(p => p.Selected);
                correctSong.Selected = false;
                correctSong.Used = true;
                DisplayCorrectSong(correctSong);
                int addScore = (-1) * (int)MyProgressBar.Value;
                _totalScore += addScore;
                _round++;
                ResultTextBlock.Text = String.Format("Score: {0}, Total Score after {1} Rounds: {2}", addScore, _round, _totalScore);
                if (_round >= 5) {
                    InstructionTextBlock.Text = String.Format("Game Over... You scored: {0}", _totalScore);
                    PlayAgainButton.Visibility = Visibility.Visible;
                } else {
                    StartCooldown();
                }
            }
        }
    }
}
