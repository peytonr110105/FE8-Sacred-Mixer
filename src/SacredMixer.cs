// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Net;
using System.Text;
using NAudio.Wave;
using System.Reflection.Metadata.Ecma335;
using System.IO;
using NAudio.Wave.SampleProviders;
using src;

List<Song> SongOverrides = new List<Song>();
int currentSongID = 0;
int previousSongID = 0;
int restartSong = 0;
//
static string getSongConfigPath(int songID)
{
    //string configPath = Directory.GetCurrentDirectory() + @"\configs\";
    //dev debugging path
    string configPath = @"c:\users\peyton\desktop\programs\FEBuilder\Music Hook" + @"\configs\";
    //append songID config file
    configPath += $"{songID}";
    configPath += ".txt";
    //Console.WriteLine("config path: " + configPath);
    return configPath;
}
//
static string getSongFilePath(int songID)
{
    string songConfigPath = getSongConfigPath(songID);
    //string songWavPath = Directory.GetCurrentDirectory();
    //dev debugging path
    string songWavPath = @"c:\users\peyton\desktop\programs\FEBuilder\Music Hook";
    //now we read the config file to see if the file specified inside of it exists
    using (StreamReader sr = File.OpenText(songConfigPath))
    {
        songWavPath += @"\audio\" + sr.ReadLine();
    }
    //Console.WriteLine("wav file path: " + songWavPath);
    return songWavPath;
}
//
static int checkSongExists(int songID)
{
    //Console.WriteLine("checking for config file...");
    //check for a song config file, then a valid .wav file
    //string directory = @"C:\Users\Peyton\Desktop\Programs\FEBuilder\Music Hook\audio\faith.wav";
    if (File.Exists(getSongConfigPath(songID)) == true)
    {
        //Console.WriteLine("Config Path OK");
        if (File.Exists(getSongFilePath(songID)) == true)
        {
            //Console.WriteLine("File Path OK");
            return songID;
        }
    }
    return -1;
}
//
IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync("localhost");
IPAddress ipAddress = ipHostInfo.AddressList[1]; // 1 to get the ipv4 version, as for me it seems to do the ipv6 as the 0th 
IPEndPoint ipEndPoint = new(ipAddress, 8888);
//
// https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-services
using Socket client = new(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
//
await client.ConnectAsync(ipEndPoint);
//establish initial connection
var message = "<CLIENT_CONNECT>";
var messageBytes = Encoding.UTF8.GetBytes(message);
_ = await client.SendAsync(messageBytes, SocketFlags.None);
Console.WriteLine($"Attempting to connect to mGBA...");
// Receive ack
var buffer = new byte[1_024];
var received = await client.ReceiveAsync(buffer, SocketFlags.None);
var response = Encoding.UTF8.GetString(buffer, 0, received);
if (response == "<ACK>")
{
    Console.WriteLine($"Connection successful.");
}
else
{
    Console.WriteLine($"Connection failed. mGBA could not be reached.");
    client.Shutdown(SocketShutdown.Both);
    return 1;
}
//Wait for song ID packets until program exits
while (true)
{
    // Receive packets from server
    buffer = new byte[1_024];
    received = await client.ReceiveAsync(buffer, SocketFlags.None);
    response = Encoding.UTF8.GetString(buffer, 0, received);
    //might be useful for debugging later if the server gets more message options
    if (response == "<ACK>")
    {
        //Console.WriteLine($"ACK");
    }
    else
    {
        Console.WriteLine($"Recieved Packet: \"{response}\"");
        //Check if the song ID that was just detected has a valid replacement
        int songID = Int32.Parse(response);
        //We pretty much always want to fade out whatever's playing when a music change happens, right?
        Song songToPause = SongOverrides.Find(x => x.outputController.PlaybackState == PlaybackState.Playing);
        //Fade current song out if there's one playing and our new song ID isn't the same as the last one
        if (songToPause != null && songID != currentSongID)
        {
            songToPause.customFadeOut();
        }
        previousSongID = currentSongID;
        currentSongID = songID;
        //Special case for muting audio
        if (songID == 0 || songID == 32767)
        {
            Console.WriteLine($"Muting audio.");
            //Stops the currently playing song if the game mutes music (0 or 7FFF)
            if (songToPause != null)
            {
                restartSong = 1;
                songToPause.outputController.Stop();
            }
        }
        else if (checkSongExists(songID) != -1)
        {
            Console.WriteLine($"Song override found. Playing...");
            Console.WriteLine($"Current Song ID: {currentSongID}");
            Console.WriteLine($"Previous Song ID: {previousSongID}");
            //Send message telling server we have a match
            //Server will mute the song
            messageBytes = Encoding.UTF8.GetBytes(response);
            _ = await client.SendAsync(messageBytes, SocketFlags.None);
            //Check if song override is in the list
            //If it isn't, create a new song entry in the list
            if (SongOverrides.Find(x => x.songID == songID) == null)
            {
                //Get song config settings and play the audio file
                string config = getSongConfigPath(songID);
                SongOverrides.Add(new Song(config, songID));
            }
            //We can do this without error checking due to the logic flow
            Song songToPlay = SongOverrides.Find(x => x.songID == songID);
            //Check if song is paused. If so, either fade in or start from scratch
            if (songToPlay.outputController.PlaybackState == PlaybackState.Paused)
            {
                //Resume playback if marked as continuous
                //Resets song positions if audio was muted by the game (0 or 7FFF)
                if (songToPlay.continuous == 0 && (previousSongID != 0 || previousSongID != 32767))
                {
                    songToPlay.customFadeIn();
                }
                //Otherwise start from scratch
                else
                {
                    Console.WriteLine("Playing file from the beginning...");
                    restartSong = 1;
                    //Have to fully stop playback before restarting file
                    songToPlay.outputController.Stop();
                    songToPlay.audioStream.Seek(0, SeekOrigin.Begin);
                    //Also have to reset volume since we faded it out previously
                    songToPlay.outputController.Volume = songToPlay.volume;
                    //songToPlay.outputController.Init(songToPlay.audioStream);
                    songToPlay.outputController.Play();
                    restartSong = 0;
                }
            }
            //Restart song if it ended (this needs to be moved to an event handler)
            //Restart song if marked as non-continuous playback
            else if (songToPlay.outputController.PlaybackState == PlaybackState.Stopped || songToPlay.continuous == 1)
            {
                Console.WriteLine("Playing file from the beginning...");
                restartSong = 1;
                //Have to fully stop playback before restarting file
                songToPlay.outputController.Stop();
                songToPlay.audioStream.Seek(0, SeekOrigin.Begin);
                //Also have to reset volume since we faded it out previously
                songToPlay.outputController.Volume = songToPlay.volume;
                //songToPlay.outputController.Init(songToPlay.audioStream);
                songToPlay.outputController.Play();
                restartSong = 0;
            }
            songToPlay.outputController.Play();
            songToPlay.outputController.PlaybackStopped += new EventHandler<StoppedEventArgs>(outputController_songStopped);
        }
        else
        {
            Console.WriteLine($"No song overrides found. Falling back to vanilla audio.");
            //Get the currently playing song, if it exists
        }
    }
}
client.Shutdown(SocketShutdown.Both);
return 0;
//loops the current song when it hits EOF
void outputController_songStopped(object sender, EventArgs e)
{
    Console.WriteLine("Playback Stopped...");
    if (restartSong == 0)
    {
        Song songToLoop = SongOverrides.Find(x => x.songID == currentSongID);
        if (songToLoop != null)
        {
            if (songToLoop.allowLooping == 1)
            {
                Console.WriteLine("Restarting audio track.");
                restartSong = 1;
                songToLoop.restartPlayback();
                if (songToLoop.outputController.PlaybackState == PlaybackState.Playing)
                {
                    Console.WriteLine("Trying to play back...");
                }
            }
        }
    }
    restartSong = 0;
}