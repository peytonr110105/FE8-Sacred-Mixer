// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Net;
using System.Text;
using NAudio.Wave;
using System.Reflection.Metadata.Ecma335;
using System.IO;
using NAudio.Wave.SampleProviders;
using src;

var outputDevice = new WaveOutEvent();
var backupOutput = new WaveOutEvent();
AudioFileReader songAllocA;
AudioFileReader songAllocB;
float songA_vol = 7.76f;
float songB_vol = 7.76f;
int currentSongID = 0;
int previousSongID = 0;
int savedSong = 0;
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
static void playSong(int songID, WaveOutEvent outputDevice)
{
    string config = getSongConfigPath(songID);
    //get wav path was kind of redundant huh
    //dev debugging path
    //string songWavPath = Directory.GetCurrentDirectory();
    string songWavPath = @"c:\users\peyton\desktop\programs\FEBuilder\Music Hook";
    //now we read the config file to see if the file specified inside of it exists
    string s;
    //config file format:
    //line 1: path to .wav file, including 'filename.wav'
    //line 2: volume (0 -> 100)
    using (StreamReader sr = File.OpenText(config))
    {
        songWavPath += @"\audio\" + sr.ReadLine();
        s = sr.ReadLine();
    }
    float volume = float.Parse(s);
    var song = new AudioFileReader(songWavPath);
    outputDevice.Init(song);
    outputDevice.Volume = volume;
    outputDevice.Play();
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
var messageBytes =  Encoding.UTF8.GetBytes(message);
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
    // Send message.
    /*Console.WriteLine("Input string and hit enter:");
    message = Console.ReadLine();
    messageBytes = Encoding.UTF8.GetBytes(message);
    _ = await client.SendAsync(messageBytes, SocketFlags.None);
    Console.WriteLine($"Socket client sent message: \"{message}\"");
    */
    // Receive packets from server
    buffer = new byte[1_024];
    received = await client.ReceiveAsync(buffer, SocketFlags.None);
    response = Encoding.UTF8.GetString(buffer, 0, received);
    if (response == "<ACK>")
    {
        Console.WriteLine($"ACK");
    }
    else
    {
        Console.WriteLine($"Recieved Packet: \"{response}\"");
        //Check if the song ID that was just detected has a valid replacement
        int songID = Int32.Parse(response);
        //cycle in new set of music if id doesn't match either of the two previous entries
        //also needs a check to make sure we're not resetting the system because of a no-replacement scenario
        if (songID != currentSongID && songID != savedSong && songID != previousSongID && songA_vol != 7.76f && songB_vol != 7.76f && checkSongExists(songID) != -1)
        {
            Console.WriteLine("Resetting system state");
            Console.WriteLine($"Current Song ID: {currentSongID}");
            Console.WriteLine($"Previous Song ID: {previousSongID}");
            Console.WriteLine($"Saved ID: {savedSong}");
            //fade out whichever song is currently playing, and halt the other
            if (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                backupOutput.Stop();
                //janky custom fade-out
                float volScale = songA_vol;
                while (volScale > 0.0f)
                {
                    volScale -= 0.01f;
                    Thread.Sleep(8);
                    if (volScale < 0.0f)
                    {
                        volScale = 0.0f;
                    }
                    outputDevice.Volume = volScale;
                }
                outputDevice.Stop();
            }
            //fade out song B
            else
            {
                outputDevice.Stop();
                float volScaleB = songB_vol;
                while (volScaleB > 0.0f)
                {
                    volScaleB -= 0.01f;
                    Thread.Sleep(8);
                    if (volScaleB < 0.0f)
                    {
                        volScaleB = 0.0f;
                    }
                    backupOutput.Volume = volScaleB;
                }
                backupOutput.Stop();
            }
            //set song volumes back to uninitialized values
            songA_vol = 7.76f;
            songB_vol = 7.76f;
        }
        //Console.WriteLine($"\"{response}\"");
        //Special case for muting audio
        Console.WriteLine($"Previous Song ID: {previousSongID}");
        Console.WriteLine($"Saved ID: {savedSong}");
        if (songID == 0 || songID == 32767)
        {
            outputDevice.Stop();
            backupOutput.Stop();
            songA_vol = 7.76f;
            songB_vol = 7.76f;
        }
        else if (checkSongExists(songID) != -1)
        {
            if (currentSongID != previousSongID || currentSongID != songID)
            {
                savedSong = currentSongID;
            }
            currentSongID = songID;
            Console.WriteLine($"Current Song ID: {currentSongID}");
            if (currentSongID == previousSongID)
            {
                //Send message telling server we have a match
                //Server will mute the song
                messageBytes = Encoding.UTF8.GetBytes(response);
                _ = await client.SendAsync(messageBytes, SocketFlags.None);
                //
                Console.WriteLine("Resuming audio override");
                outputDevice.Play();
                //janky custom fade-in
                float volScale = 0.0f;
                while (volScale < songA_vol)
                {
                    volScale += 0.01f;
                    Thread.Sleep(8);
                    if (volScale > songA_vol)
                    {
                        volScale = songA_vol;
                    }
                    outputDevice.Volume = volScale;
                }
            }
            else
            {
                //if (currentSongID != previousSongID)
                previousSongID = currentSongID;
                Console.WriteLine($"Song override found. Attempting to play...");
                //Send message telling server we have a match
                //Server will mute the song
                messageBytes = Encoding.UTF8.GetBytes(response);
                _ = await client.SendAsync(messageBytes, SocketFlags.None);
                //Get song config settings and play the audio file
                //playSong(songID, outputDevice);
                string config = getSongConfigPath(songID);
                //get wav path was kind of redundant huh
                //dev debugging path
                //string songWavPath = Directory.GetCurrentDirectory();
                string songWavPath = @"c:\users\peyton\desktop\programs\FEBuilder\Music Hook";
                //now we read the config file to see if the file specified inside of it exists
                string s,s2;
                //config file format:
                //line 1: path to .wav file, including 'filename.wav'
                //line 2: volume (0 -> 100)
                using (StreamReader sr = File.OpenText(config))
                {
                    songWavPath += @"\audio\" + sr.ReadLine();
                    s = sr.ReadLine();
                    s2 = sr.ReadLine();

                }
                int songPriority = Int32.Parse(s2);
                //if a song is already playing we need to use the secondary buffer
                //alternatively, mark file as always playing as secondary audio (ie battle music)
                if (outputDevice.PlaybackState == PlaybackState.Playing || songPriority == 1)
                {
                    //janky custom fade-out
                    float volScale = songA_vol;
                    Console.WriteLine("Fading out primary stream");
                    while (volScale > 0.0f)
                    {
                        volScale -= 0.01f;
                        Thread.Sleep(8);
                        if (volScale < 0.0f)
                        {
                            volScale = 0.0f;
                        }
                        outputDevice.Volume = volScale;
                    }
                    Console.WriteLine("Switching to backup output");
                    outputDevice.Pause();
                    songB_vol = float.Parse(s);
                    var songB = new AudioFileReader(songWavPath);
                    backupOutput.Init(songB);
                    backupOutput.Volume = songB_vol;
                    backupOutput.Play();
                }
                else
                {
                    if (outputDevice.PlaybackState != PlaybackState.Playing && songA_vol != 7.76f)
                    {
                        //janky custom fade-out
                        //fade out song B
                        //only fade out if playing
                        Console.WriteLine("Fading out backup stream");
                        if (backupOutput.PlaybackState == PlaybackState.Playing)
                        {
                            float volScaleB = songB_vol;
                            while (volScaleB > 0.0f)
                            {
                                volScaleB -= 0.01f;
                                Thread.Sleep(8);
                                if (volScaleB < 0.0f)
                                {
                                    volScaleB = 0.0f;
                                }
                                backupOutput.Volume = volScaleB;
                            }
                            backupOutput.Stop();
                        }
                        outputDevice.Play();
                        //janky custom fade-in
                        //fade song A back in
                        float volScale = 0.0f;
                        Console.WriteLine("Fading in primary stream");
                        while (volScale < songA_vol)
                        {
                            volScale += 0.01f;
                            Thread.Sleep(8);
                            if (volScale > songA_vol)
                            {
                                volScale = songA_vol;
                            }
                            outputDevice.Volume = volScale;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Playing as primary output");
                        songA_vol = float.Parse(s);
                        var song = new AudioFileReader(songWavPath);
                        outputDevice.Init(song);
                        outputDevice.Volume = songA_vol;
                        outputDevice.Play();
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"No song overrides found. Falling back to vanilla audio.");
            if (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                //janky custom fade-out
                float volScale = songA_vol;
                while (volScale > 0.0f)
                {
                    volScale -= 0.01f;
                    Thread.Sleep(8);
                    if (volScale < 0.0f)
                    {
                        volScale = 0.0f;
                    }
                    outputDevice.Volume = volScale;
                }
                outputDevice.Pause();
            }
            backupOutput.Stop();
        }
    }
}
client.Shutdown(SocketShutdown.Both);
return 0;