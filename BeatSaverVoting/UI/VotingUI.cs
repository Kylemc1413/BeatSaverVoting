using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaverVoting.Utilities;
using Steamworks;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace BeatSaverVoting.UI
{
    public class VotingUI : NotifiableSingleton<VotingUI>
    {
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

        private static BeatSaverSharp.BeatSaver m_BeatSaberClient = null;
        internal IBeatmapLevel _lastSong;
        private OpenVRHelper openVRHelper;
        private BeatSaverSharp.Beatmap _lastBeatSaverSong;

        internal void Setup()
        {
            var resultsView = Resources.FindObjectsOfTypeAll<ResultsViewController>().FirstOrDefault();
            if (!resultsView)
                return;

            if (m_BeatSaberClient == null)
                m_BeatSaberClient = new BeatSaverSharp.BeatSaver();

            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BeatSaverVoting.UI.votingUI.bsml"), resultsView.gameObject, this);
            resultsView.didActivateEvent += ResultsView_didActivateEvent;

            UnityEngine.UI.Image upArrow = upButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();
            UnityEngine.UI.Image downArrow = downButton.transform.Find("Arrow")?.GetComponent<UnityEngine.UI.Image>();

            if (upArrow != null && downArrow != null)
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
            var hash = SongCore.Utilities.Hashing.GetCustomLevelHash(level as CustomPreviewBeatmapLevel).ToLower();

            /// disable buttons until populate task is done
            if (openVRHelper == null) openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();
            bool canVote = (/*PluginConfig.apiAccessToken != PluginConfig.apiTokenPlaceholder ||*/ (openVRHelper.vrPlatformSDK == VRPlatformSDK.OpenVR || Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc")));
            UpInteractable = canVote;
            DownInteractable = canVote;

            /// prepare partial beatmap
            m_BeatSaberClient.Hash(hash).ContinueWith((p_TaskResult) =>
            {
                if (p_TaskResult.Status != System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    Logging.Log.Error($"Failed to get beatmap for hash {hash} task status {p_TaskResult.Status}");
                    return;
                }

                _lastBeatSaverSong = p_TaskResult.Result;

                HMMainThreadDispatcher.instance.Enqueue(() =>
                {
                    voteText.text = (_lastBeatSaverSong.Stats.UpVotes - _lastBeatSaverSong.Stats.DownVotes).ToString();
                    Logging.Log.Debug("SET VOTESSSS " + voteText.text);
                    if (Plugin.votedSongs.TryGetValue(_lastBeatSaverSong.Hash.ToLower(), out var vote))
                    {
                        switch (vote.voteType)
                        {
                            case Plugin.VoteType.Upvote:    { UpInteractable    = false; } break;
                            case Plugin.VoteType.Downvote:  { DownInteractable  = false; } break;
                        }
                    }
                });
            });

            yield return null;
        }


        private void VoteForSong(bool upvote)
        {
            try
            {
                if (openVRHelper == null)
                    openVRHelper = Resources.FindObjectsOfTypeAll<OpenVRHelper>().First();

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
                yield break;
            }

            if (_lastBeatSaverSong == null)
                yield break;

            /// disable until vote result
            UpInteractable = false;
            DownInteractable = false;
            voteText.text = "Voting...";

            Logging.Log.Debug($"Getting a ticket...");

            var steamId = SteamUser.GetSteamID();

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
                                voteText.text = "User does not\nhave license";
                                yield break;

                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultHasLicense:
                                if (SteamHelper.Instance.m_GetAuthSessionTicketResponse == null)
                                {
                                    SteamHelper.Instance.m_GetAuthSessionTicketResponse = Callback<GetAuthSessionTicketResponse_t>.Create((GetAuthSessionTicketResponse_t response) =>
                                    {
                                        if (SteamHelper.Instance.lastTicket == response.m_hAuthTicket)
                                            SteamHelper.Instance.lastTicketResult = response.m_eResult;
                                    });

                                }

                                SteamHelper.Instance.lastTicket = SteamUser.GetAuthSessionTicket(authTicket, 1024, out length);
                                if (SteamHelper.Instance.lastTicket != HAuthTicket.Invalid)
                                    Array.Resize(ref authTicket, (int)length);

                                break;
                            case EUserHasLicenseForAppResult.k_EUserHasLicenseResultNoAuth:
                                voteText.text = "User is not\nauthenticated";
                                yield break;
                        }
                        break;

                    default:
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
                UpInteractable   = true;
                DownInteractable = true;
                voteText.text    = "Callback\ntimeout";
                yield break;
            }

            SteamHelper.Instance.lastTicketResult = EResult.k_EResultRevoked;

            Logging.Log.Debug($"Voting...");

            Action<Task<bool>> voteCallback = (result) => {
                if (result.Status != System.Threading.Tasks.TaskStatus.RanToCompletion
                   || result.Exception != null)
                {
                    Logging.Log.Error($"Failed to vote beatmap for hash {_lastBeatSaverSong.Hash}");
                    if (result.Exception != null)
                        Logging.Log.Error(result.Exception);

                    HMMainThreadDispatcher.instance.Enqueue(() =>
                    {
                        if (result.Exception.InnerException is BeatSaverSharp.Exceptions.InvalidSteamIDException)
                            voteText.text = "Invalid\nsession";
                        if (result.Exception.InnerException is BeatSaverSharp.Exceptions.InvalidTicketException)
                            voteText.text = "Invalid\nauth ticket";
                        else
                            voteText.text = "Network\nerror";
                    });
                    return;
                }

                HMMainThreadDispatcher.instance.Enqueue(() =>
                {
                    if (result.Result)
                    {
                        voteText.text = (_lastBeatSaverSong.Stats.UpVotes - _lastBeatSaverSong.Stats.DownVotes).ToString();

                        UpInteractable   = !upvote;
                        DownInteractable = upvote;

                        var voteType = upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote;
                        if (Plugin.votedSongs.TryGetValue(_lastBeatSaverSong.Hash.ToLower(), out var vote))
                        {
                            if (vote.voteType != voteType)
                            {
                                vote.voteType = voteType;
                                Plugin.WriteVotes();
                            }
                        }
                        else
                        {
                            Plugin.votedSongs.Add(_lastBeatSaverSong.Hash.ToLower(), new Plugin.SongVote(_lastBeatSaverSong.Key, voteType));
                            Plugin.WriteVotes();
                        }
                    }
                    else
                    {
                        UpInteractable      = true;
                        DownInteractable    = true;
                        voteText.text       = "Error\n";
                    }
                });
            };

            if (upvote)
                _lastBeatSaverSong.VoteUp(steamId.m_SteamID.ToString(), authTicket).ContinueWith(voteCallback);
            else
                _lastBeatSaverSong.VoteDown(steamId.m_SteamID.ToString(), authTicket).ContinueWith(voteCallback);
        }
    }
}
