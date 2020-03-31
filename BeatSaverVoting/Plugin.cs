using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using TMPro;
using UnityEngine;
using System.Text;
using UnityEngine.SceneManagement;
using IPA;
using IPALogger = IPA.Logging.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
namespace BeatSaverVoting
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public enum VoteType { Upvote, Downvote };

        public struct SongVote
        {
            public string key;
            [JsonConverter(typeof(StringEnumConverter))]
            public VoteType voteType;

            public SongVote(string key, VoteType voteType)
            {
                this.key = key;
                this.voteType = voteType;
            }
        }

        internal static string beatsaverURL = "https://beatsaver.com";
        internal static string votedSongsPath = $"{Environment.CurrentDirectory}/UserData/votedSongs.json";
        internal static Dictionary<string, SongVote> votedSongs = new Dictionary<string, SongVote>();
        [OnStart]
        public void OnApplicationStart()
        {
            BS_Utils.Utilities.BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            BS_Utils.Utilities.BSEvents.gameSceneLoaded += BSEvents_gameSceneLoaded1;

            if (!File.Exists(votedSongsPath))
            {
                File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
            }
            else
            {
                votedSongs = JsonConvert.DeserializeObject<Dictionary<string, SongVote>>(File.ReadAllText(votedSongsPath, Encoding.UTF8));
            }
        }

        private void BSEvents_gameSceneLoaded1()
        {
            UI.VotingUI.instance._lastSong = BS_Utils.Plugin.LevelData.GameplayCoreSceneSetupData.difficultyBeatmap.level;
        }

        private void BSEvents_menuSceneLoadedFresh()
        {
         //   Utilities.Logging.Log.Info("Menu Scene Loaded");
            UI.VotingUI.instance.Setup();
        }

        [Init]
        public void Init(IPALogger pluginLogger)
        {
            Utilities.Logging.Log = pluginLogger;
        }

        public void OnApplicationQuit()
        {

        }
        public static void WriteVotes()
        {
            File.WriteAllText(votedSongsPath, JsonConvert.SerializeObject(votedSongs), Encoding.UTF8);
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {

        }

        public void OnSceneUnloaded(Scene scene)
        {

        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {

        }

        public void OnUpdate()
        {

        }

        public void OnFixedUpdate()
        {

        }

    }
}
