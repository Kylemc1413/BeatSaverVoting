using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BS_Utils.Utilities;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.Networking;
using BeatSaverVoting.Utilities;
using Newtonsoft.Json.Linq;
using Steamworks;

namespace BeatSaverVoting.UI
{
    public class VotingUI : NotifiableSingleton<VotingUI>
    {

        [Serializable]
        private struct Payload
        {
            public string steamID;
            public string ticket;
            public int direction;
        }

        internal IBeatmapLevel _lastSong;
        private OpenVRHelper openVRHelper;
        private bool _firstVote;
        private Song _lastBeatSaverSong;
        private string userAgent = $"BeatSaverVoting/{Assembly.GetExecutingAssembly().GetName().Version}";
        [UIComponent("voteTitle")]
        public TextMeshProUGUI voteTitle;
        [UIComponent("voteText")]
        public TextMeshProUGUI voteText;
        [UIComponent("upButton")]
        public Transform upButton;
        [UIComponent("downButton")]
        public Transform downButton;
        private bool upInteractable = true;
        [UIValue("upInteractable")]
        public bool UpInteractable
        {
            get => upInteractable;
            set
            {
                upInteractable = value;
                NotifyPropertyChanged();
            }
        }
        private bool downInteractable = true;
        [UIValue("downInteractable")]
        public bool DownInteractable
        {
            get => downInteractable;
            set
            {
                downInteractable = value;
                NotifyPropertyChanged();
            }
        } 



        internal void Setup()
        {
            var resultsView = Resources.FindObjectsOfTypeAll<ResultsViewController>().FirstOrDefault();
            if (!resultsView) return;
            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BeatSaverVoting.UI.votingUI.bsml"), resultsView.gameObject, this);
            resultsView.didActivateEvent += ResultsView_didActivateEvent;
            UnityEngine.UI.Image upArrow = upButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image downArrow = downButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            if(upArrow != null && downArrow != null)
            {
                upArrow.color = new Color(0.341f, 0.839f, 0.341f);
                downArrow.color = new Color(0.984f, 0.282f, 0.305f);
            }
        }

        private void ResultsView_didActivateEvent(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            //Utilities.Logging.Log.Info("Initializing VotingUI");
            GetVotesForMap();
        }

        [UIAction("up-pressed")]
        private void UpvoteButtonPressed()
        {
            VoteForSong(true);
        }
        [UIAction("down-pressed")]
        private void DownvoteButtonPressed()
        {
            VoteForSong(false);
        }

        private void GetVotesForMap()
        {
            if(!(_lastSong is CustomPreviewBeatmapLevel))
            {
                downButton.gameObject.SetActive(false);
                upButton.gameObject.SetActive(false);
                voteText.text = "";
                voteTitle.text = "";
                return;
            }
            voteText.text = "Loading...";
            StartCoroutine(GetRatingForSong(_lastSong));


        }

        private IEnumerator GetRatingForSong(IBeatmapLevel level)
        {
            //     Plugin.log.Info($"{PluginConfig.beatsaverURL}/api/maps/by-hash/{SongCore.Utilities.Hashing.GetCustomLevelHash(level as CustomPreviewBeatmapLevel).ToLower()}");
            UnityWebRequest www = UnityWebRequest.Get($"{Plugin.beatsaverURL}/api/maps/by-hash/{SongCore.Utilities.Hashing.GetCustomLevelHash(level as CustomPreviewBeatmapLevel).ToLower()}");
            www.SetRequestHeader("user-agent", userAgent);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Logging.Log.Error($"Unable to connect to {Plugin.beatsaverURL}! " + (www.isNetworkError ? $"Network error: {www.error}" : (www.isHttpError ? $"HTTP error: {www.error}" : "Unknown error")));
            }
            else
            {
                try
                {
                    _firstVote = true;
                    JObject jNode = JObject.Parse(www.downloadHandler.text);

                    if (jNode.Children().Count() > 0)
                    {
                        _lastBeatSaverSong = new Song((JObject)jNode);

                        voteText.text = (_lastBeatSaverSong.upVotes - _lastBeatSaverSong.downVotes).ToString();
                        if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
                        bool canVote = (/*PluginConfig.apiAccessToken != PluginConfig.apiTokenPlaceholder ||*/ (openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR || Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc")));

                        UpInteractable = canVote;
                        DownInteractable = canVote;

                        //           _reviewButton.interactable = true;
                        string lastLevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(_lastSong as CustomPreviewBeatmapLevel).ToLower();
                        if (Plugin.votedSongs.ContainsKey(lastLevelHash))
                        {
                            switch (Plugin.votedSongs[lastLevelHash].voteType)
                            {
                                case Plugin.VoteType.Upvote: { UpInteractable = false; } break;
                                case Plugin.VoteType.Downvote: { DownInteractable = false; } break;
                            }
                        }
                    }
                    else
                    {
                        Logging.Log.Error("Song doesn't exist on BeatSaver!");
                    }
                }
                catch (Exception e)
                {
                    Logging.Log.Critical("Unable to get song rating! Excpetion: " + e);
                }
            }
        }


        private void VoteForSong(bool upvote)
        {
            //      if(PluginConfig.apiAccessToken != PluginConfig.apiTokenPlaceholder && !string.IsNullOrWhiteSpace(PluginConfig.apiAccessToken))
            //      {
            //          StartCoroutine(VoteWithAccessToken(upvote));
            //      }
            //else
            try
            {
            if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
            if ((openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR || Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc")))
            {
                StartCoroutine(VoteWithSteamID(upvote));
            }
            }
            catch(Exception ex)
            {
                Logging.Log.Warn("Failed To Vote For Song");
            }

        }

        private IEnumerator VoteWithSteamID(bool upvote)
        {
            if (!SteamManager.Initialized)
            {
                Logging.Log.Error($"SteamManager is not initialized!");
            }
            void OnAuthTicketResponse(GetAuthSessionTicketResponse_t response)
            {
                if (SteamHelper.Instance.lastTicket == response.m_hAuthTicket)
                {
                    SteamHelper.Instance.lastTicketResult = response.m_eResult;
                }
            };

            UpInteractable = false;
            DownInteractable = false;
            voteText.text = "Voting...";
            Logging.Log.Debug($"Getting a ticket...");

            var steamId = SteamUser.GetSteamID();
            string authTicketHexString = "";

            byte[] authTicket = new byte[1024];
            var authTicketResult = SteamUser.GetAuthSessionTicket(authTicket, 1024, out var length);
            if (authTicketResult != HAuthTicket.Invalid)
            {
                var beginAuthSessionResult = SteamUser.BeginAuthSession(authTicket, (int)length, steamId);
                switch (beginAuthSessionResult)
                {
                    case EBeginAuthSessionResult.k_EBeginAuthSessionResultOK:
                        var result = SteamUser.UserHasLicenseForApp(steamId, new AppId_t(620980));

                        SteamUser.EndAuthSession(steamId);

                        switch (result)
                        {
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultDoesNotHaveLicense:
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "User does not\nhave license";
                                yield break;
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultHasLicense:
                                if (SteamHelper.Instance.m_GetAuthSessionTicketResponse == null)
                                    SteamHelper.Instance.m_GetAuthSessionTicketResponse = Callback<GetAuthSessionTicketResponse_t>.Create(OnAuthTicketResponse);


                                SteamHelper.Instance.lastTicket = SteamUser.GetAuthSessionTicket(authTicket, 1024, out length);
                                if (SteamHelper.Instance.lastTicket != HAuthTicket.Invalid)
                                {
                                    Array.Resize(ref authTicket, (int)length);
                                    authTicketHexString = BitConverter.ToString(authTicket).Replace("-", "");
                                }

                                break;
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultNoAuth:
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "User is not\nauthenticated";
                                yield break;
                        }
                        break;
                    default:
                        UpInteractable = false;
                        DownInteractable = false;
                        voteText.text = "Auth\nfailed";
                        yield break;
                }
            }

            Logging.Log.Debug("Waiting for Steam callback...");

            float startTime = Time.time;
            yield return new WaitWhile(() => { return SteamHelper.Instance.lastTicketResult != EResult.k_EResultOK && (Time.time - startTime) < 20f; });

            if (SteamHelper.Instance.lastTicketResult != EResult.k_EResultOK)
            {
                Logging.Log.Error($"Auth ticket callback timeout");
                UpInteractable = true;
                DownInteractable = true;
                voteText.text = "Callback\ntimeout";
                yield break;
            }

            SteamHelper.Instance.lastTicketResult = EResult.k_EResultRevoked;

            Logging.Log.Debug($"Voting...");

            Payload payload = new Payload() { steamID = steamId.m_SteamID.ToString(), ticket = authTicketHexString, direction = (upvote ? 1 : -1) };
            string json = JsonUtility.ToJson(payload);
           // Logging.Log.Info(json);
            UnityWebRequest voteWWW = UnityWebRequest.Post($"{Plugin.beatsaverURL}/api/vote/steam/{_lastBeatSaverSong.key}", json);

         //   Logging.Log.Info($"{Plugin.beatsaverURL}/api/vote/steam/{_lastBeatSaverSong.hash}");
         //   Logging.Log.Info($"{Plugin.beatsaverURL}/api/vote/steam/{_lastBeatSaverSong.key}");

            byte[] jsonBytes = new System.Text.UTF8Encoding().GetBytes(json);
            voteWWW.uploadHandler = new UploadHandlerRaw(jsonBytes);
            voteWWW.SetRequestHeader("Content-Type", "application/json");
            voteWWW.SetRequestHeader("user-agent", userAgent);
            voteWWW.timeout = 30;
            yield return voteWWW.SendWebRequest();

            if (voteWWW.isNetworkError)
            {
                Logging.Log.Error(voteWWW.error);
                voteText.text = voteWWW.error;
            }
            else
            {
                if (!_firstVote)
                {
                    yield return new WaitForSecondsRealtime(2f);
                }

                _firstVote = false;

                if (voteWWW.responseCode >= 200 && voteWWW.responseCode <= 299)
                {
                    //              Plugin.log.Info(voteWWW.downloadHandler.text);
                    JObject node = JObject.Parse(voteWWW.downloadHandler.text);
                    //         Plugin.log.Info(((int)node["stats"]["upVotes"]).ToString() + " -- " + ((int)(node["stats"]["downVotes"])).ToString());
                    voteText.text = (((int)node["stats"]["upVotes"]) - ((int)node["stats"]["downVotes"])).ToString();

                    if (upvote)
                    {
                        UpInteractable = false;
                        DownInteractable = true;
                    }
                    else
                    {
                        DownInteractable = false;
                        UpInteractable = true;
                    }
                    string lastlevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(_lastSong as CustomPreviewBeatmapLevel).ToLower();
                    if (!Plugin.votedSongs.ContainsKey(lastlevelHash))
                    {
                        Plugin.votedSongs.Add(lastlevelHash, new Plugin.SongVote(_lastBeatSaverSong.key, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote));
                        Plugin.WriteVotes();
                    }
                    else if (Plugin.votedSongs[lastlevelHash].voteType != (upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote))
                    {
                        Plugin.votedSongs[lastlevelHash] = new Plugin.SongVote(_lastBeatSaverSong.key, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote);
                        Plugin.WriteVotes();
                    }
                }
                else switch (voteWWW.responseCode)
                    {
                        case 500:
                            {
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "Server \nerror";
                                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            }; break;
                        case 401:
                            {
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "Invalid\nauth ticket";
                                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            }; break;
                        case 404:
                            {
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "Beatmap not\found";
                                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            }; break;
                        case 400:
                            {
                                UpInteractable = false;
                                DownInteractable = false;
                                voteText.text = "Bad\nrequest";
                                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            }; break;
                        default:
                            {
                                UpInteractable = true;
                                DownInteractable = true;
                                voteText.text = "Error\n" + voteWWW.responseCode;
                                Logging.Log.Error("Error: " + voteWWW.downloadHandler.text);
                            }; break;
                    }
            }
        }
    }
}
