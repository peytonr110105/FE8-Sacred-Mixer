using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace src
{
    internal class Song
    {
        public string configPath;
        public string wavPath;
        public int songID;
        public int continuous;
        public float volume;
        public WaveOutEvent outputController = new WaveOutEvent();
        public AudioFileReader audioStream;
        //Fades out song by manually settings its volume every 8ms
        //This needs to be made async!!!
        public void customFadeOut()
        {
            //janky custom fade-out
            float volScale = volume;
            while (volScale > 0.0f)
            {
                volScale -= 0.01f;
                Thread.Sleep(8);
                if (volScale < 0.0f)
                {
                    volScale = 0.0f;
                }
                outputController.Volume = volScale;
            }
            outputController.Pause();
        }
        //same as above but for fade in
        public void customFadeIn()
        {
            outputController.Play();
            //janky custom fade-in
            float volScale = 0.0f;
            while (volScale < volume)
            {
                volScale += 0.01f;
                Thread.Sleep(8);
                if (volScale > volume)
                {
                    volScale = volume;
                }
                outputController.Volume = volScale;
            }
        }
        public Song(string conf, int id)
        {
            songID = id;
            configPath = conf;
            //Fills out song information with config path passed
            //now we read the config file to see if the file specified inside of it exists
            string s;
            //config file format:
            //line 1: path to .wav file, including 'filename.wav'
            //line 2: volume (0 -> 100)
            //line 3: non-continuous playback (if 1, always start song from the beginning)
            //replace this with the directory call for release
            wavPath = @"c:\users\peyton\desktop\programs\FEBuilder\Music Hook";
            using (StreamReader sr = File.OpenText(configPath))
            {
                wavPath += @"\audio\" + sr.ReadLine();
                s = sr.ReadLine();
                volume = float.Parse(s);
                s = sr.ReadLine();
                continuous = Int32.Parse(s);
            }
            audioStream = new AudioFileReader(wavPath);
            outputController.Init(audioStream);
            outputController.Volume = volume;
        }
        public void restartPlayback()
        {
            outputController.Stop();
            audioStream = new AudioFileReader(wavPath);
            outputController.Volume = volume;
            outputController.Init(audioStream);
            outputController.Play();
        }
    }
}
