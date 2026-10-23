using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SyntyCombined.World;

namespace SyntyCombined.BlackMarket
{
    public enum NPCRoleType
    {
        StoryFixer,
        WeaponsMerchant,
        Ripperdoc,
        ShipMechanic,
        FoodVendor,
        ContrabandDealer,
        StationEnforcer,
        MarketPatron
    }

    [System.Serializable]
    public class DialogueEntry
    {
        [TextArea(2, 4)]
        public string dialogueText;
        public string choiceOptionText; // Optional response text
        public string speakerMood = "Neutral";
    }

    /// <summary>
    /// Interactive Black Market NPC & Shopkeeper controller.
    /// Manages interaction trigger ([E]), proximity HUD pill, dialogue sequence,
    /// dynamic look-at orientation, and story quest coordinate decryption.
    /// </summary>
    [ExecuteAlways]
    public class BlackMarketNPC : MonoBehaviour
    {
        [Header("NPC Identity & Role")]
        public string characterName = "Kaelen Voss";
        public NPCRoleType roleType = NPCRoleType.StoryFixer;
        public string roleBadgeTitle = "STORY // INFORMANT";
        public Color badgeColor = new Color(0.22f, 0.74f, 0.97f); // Cyan
        public Sprite portraitIcon;

        [Header("Dialogue Interaction")]
        [Tooltip("If true, the player can interact and talk with this NPC. When false, no prompt is displayed and interaction is disabled.")]
        public bool canTalk = false;

        [Header("Story Quest Integration")]
        public bool isStoryObjectiveNPC = false;
        [Tooltip("Mission objective text unlocked upon completing this conversation")]
        public string unlockObjectiveText = "MISSION UPDATED: Board Starship & Hit [R] to Warp to Mars Cydonia Vault";

        [Header("Investigation Lead")]
        [Tooltip("If set, finishing this NPC's dialogue cross-references a lead on Voss")]
        public string leadId = "";
        public string leadTitle = "";
        [Tooltip("Short HUD toast shown when the lead is learned")]
        public string leadInsight = "";

        [Header("Dialogue Content (Phase 1: Task Assignment)")]
        public List<DialogueEntry> dialogueEntries = new List<DialogueEntry>();

        [Header("Dialogue Content (Phase 2: Return with Core & Betrayal)")]
        public List<DialogueEntry> returnDialogueEntries = new List<DialogueEntry>();

        [Header("Waiting Dialogue (While Task In Progress)")]
        [TextArea(2, 4)]
        public string waitingDialogueText = "Retrieve all 4 Antimatter Cores from the orbital space debris outside the dome first, Alita. No cores, no Mars coordinates.";

        [Header("Interaction Radius")]
        public float interactionRange = 4.8f;
        public Vector3 promptOffset = new Vector3(0f, 2.1f, 0f);

        [Header("Audio")]
        [Header("Audio")]
        public AudioClip interactionSFX;
        public AudioClip[] voiceChatterSFX;

        [Header("Animation & Posture")]
        public Animator animator;
        [Tooltip("Base idle style: 0=Casual, 1=Phone Scroll, 2=Phone Call, 3=Arms Folded, 6=Thoughtful Chin Scratch, 7=Hands On Hips, 14=Lean Back, 26=Drink Hold, 28=Aggressive Enforcer, etc.")]
        public int defaultIdleID = 0;
        public bool playAmbientFidgets = true;
        public float fidgetIntervalMin = 6.0f;
        public float fidgetIntervalMax = 12.0f;
        private float _fidgetTimer = 0f;
        private bool _isPlayingFidget = false;
        private float _fidgetDuration = 0f;

        // Runtime state
        private Transform _playerTransform;
        private bool _isPlayerInRange = false;
        private bool _isConversing = false;
        private int _currentDialogueIndex = 0;
        private Quaternion _originalRotation;
        private List<DialogueEntry> _activeDialogueList;
        private BlackMarketNPCWanderer _wanderer;

        public static bool MarsCoordinatesUnlocked = false;

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                SetupAnimator();
                if (animator != null)
                {
                    animator.Update(0.05f);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                SetupAnimator();
                if (animator != null)
                {
                    animator.Update(0.05f);
                }
            }
        }
#endif

        private void Start()
        {
            _originalRotation = transform.rotation;
            _wanderer = GetComponent<BlackMarketNPCWanderer>();
            SetupAnimator();
            FindPlayer();
        }

        private void SetupAnimator()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null)
            {
                if (animator.runtimeAnimatorController == null)
                {
#if UNITY_EDITOR
                    animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/PolygonCyberCity/Animations/AC_CyberNPC.controller");
#endif
                }

                if (_wanderer == null)
                {
                    ApplyIdlePosture(defaultIdleID);
                }
            }

            _fidgetTimer = Random.Range(fidgetIntervalMin, fidgetIntervalMax);
        }

        private bool HasParameter(string paramName)
        {
            if (animator == null) return false;
            foreach (var p in animator.parameters)
            {
                if (p.name == paramName) return true;
            }
            return false;
        }

        public void ApplyIdlePosture(int idleId)
        {
            if (animator == null) return;
            if (_wanderer != null && _wanderer.IsMoving) return;
            if (HasParameter("RandomIdle")) animator.SetInteger("RandomIdle", idleId);
            if (HasParameter("IsWalking")) animator.SetBool("IsWalking", false);
            if (HasParameter("Speed")) animator.SetFloat("Speed", 0f);
        }

        private void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
            else
            {
                var alita = GameObject.Find("Alita") ?? GameObject.Find("Alita_Hero") ?? GameObject.Find("Alita_BattleAngel_Hero");
                if (alita != null)
                {
                    _playerTransform = alita.transform;
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Ambient background NPCs do not interact, show prompts, or track player
            if (!canTalk && !isStoryObjectiveNPC)
            {
                UpdateAmbientAnimation();
                return;
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Disable interaction if story moved past betrayal
            if (isStoryObjectiveNPC && BlackMarketStoryCombatDirector.Instance != null)
            {
                var state = BlackMarketStoryCombatDirector.Instance.currentState;
                if (state == BlackMarketStoryState.BetrayalAmbush ||
                    state == BlackMarketStoryState.FleeAcrossCatwalk ||
                    state == BlackMarketStoryState.CatwalkCollapse_Fall ||
                    state == BlackMarketStoryState.LowerTrenchCombat ||
                    state == BlackMarketStoryState.EscapeToStarship ||
                    state == BlackMarketStoryState.HyperspaceWarp)
                {
                    if (_isConversing) CloseConversation();
                    _isPlayerInRange = false;
                    return;
                }
            }

            float dx = transform.position.x - _playerTransform.position.x;
            float dz = transform.position.z - _playerTransform.position.z;
            float dy = Mathf.Abs(transform.position.y - _playerTransform.position.y);
            float horizDist = Mathf.Sqrt(dx * dx + dz * dz);
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerInRange = (horizDist <= interactionRange && dy <= 3.2f);

            // Pause wanderer ONLY when actively conversing with player
            if (_wanderer != null)
            {
                _wanderer.SetPaused(_isConversing);
            }

            // Turn to face player when conversing, or when in close proximity (for stationary NPCs only)
            if (_isConversing || (_isPlayerInRange && _wanderer == null))
            {
                Vector3 lookDir = (_playerTransform.position - transform.position);
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
                }
            }

            // Check for interaction input [F] or [E] when in range
            if (_isPlayerInRange)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
                {
                    if (!_isConversing)
                    {
                        StartConversation();
                    }
                    else
                    {
                        // Note: When CyberpunkDialogueHUD is active, it handles advance input ([E], [F], [Space], Click),
                        // typewriter instant completion, and invokes AdvanceConversation() via callback.
                        // Only advance directly here if no dialogue HUD is active to prevent double-skipping.
                        if (Alita.UI.CyberpunkDialogueHUD.Instance == null || !Alita.UI.CyberpunkDialogueHUD.Instance.isDialogueActive)
                        {
                            AdvanceConversation();
                        }
                    }
                }
            }
            else
            {
                // Return to original idle rotation if static
                if (!_isConversing && _wanderer == null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, _originalRotation, Time.deltaTime * 2.0f);
                }
            }

            // Close conversation if player walked away
            if (_isConversing && dist > interactionRange * 1.5f)
            {
                CloseConversation();
            }

            UpdateAmbientAnimation();
        }

        private void UpdateAmbientAnimation()
        {
            if (animator == null || !playAmbientFidgets) return;
            if (_isConversing) return;
            if (_wanderer != null) return; // Wanderers manage their own synchronized waypoint idle system

            _fidgetTimer -= Time.deltaTime;
            if (_fidgetTimer <= 0f)
            {
                if (_isPlayingFidget)
                {
                    ApplyIdlePosture(defaultIdleID);
                    _isPlayingFidget = false;
                    _fidgetTimer = Random.Range(fidgetIntervalMin, fidgetIntervalMax);
                }
                else
                {
                    int fidgetId = PickRoleFidget();
                    if (HasParameter("RandomIdle")) animator.SetInteger("RandomIdle", fidgetId);
                    _isPlayingFidget = true;
                    _fidgetTimer = Random.Range(2.5f, 4.2f);
                }
            }
        }

        private int PickRoleFidget()
        {
            switch (roleType)
            {
                case NPCRoleType.StationEnforcer:
                    int[] enfFidgets = { 4, 19, 28, 7 };
                    return enfFidgets[Random.Range(0, enfFidgets.Length)];
                case NPCRoleType.WeaponsMerchant:
                    int[] wpnFidgets = { 6, 4, 5, 3 };
                    return wpnFidgets[Random.Range(0, wpnFidgets.Length)];
                case NPCRoleType.ShipMechanic:
                    int[] techFidgets = { 5, 18, 1, 7 };
                    return techFidgets[Random.Range(0, techFidgets.Length)];
                case NPCRoleType.Ripperdoc:
                    int[] docFidgets = { 1, 6, 11, 4 };
                    return docFidgets[Random.Range(0, docFidgets.Length)];
                case NPCRoleType.FoodVendor:
                    int[] cookFidgets = { 27, 25, 23, 4 };
                    return cookFidgets[Random.Range(0, cookFidgets.Length)];
                case NPCRoleType.ContrabandDealer:
                    int[] dealerFidgets = { 22, 6, 8, 14 };
                    return dealerFidgets[Random.Range(0, dealerFidgets.Length)];
                default:
                    int[] genFidgets = { 4, 5, 18, 21, 22, 26 };
                    return genFidgets[Random.Range(0, genFidgets.Length)];
            }
        }

        public void StartConversation()
        {
            if (!canTalk && !isStoryObjectiveNPC) return;

            _isConversing = true;
            _currentDialogueIndex = 0;

            if (animator != null)
            {
                if (HasParameter("Gesture")) animator.SetTrigger("Gesture");
                if (HasParameter("RandomIdle")) animator.SetInteger("RandomIdle", 0);
            }

            if (interactionSFX != null)
            {
                AudioSource.PlayClipAtPoint(interactionSFX, transform.position, 0.8f);
            }

            // Select appropriate dialogue list based on director state
            if (isStoryObjectiveNPC && BlackMarketStoryCombatDirector.Instance != null)
            {
                var state = BlackMarketStoryCombatDirector.Instance.currentState;
                if (state == BlackMarketStoryState.InvestigateMarket && !BlackMarketStoryCombatDirector.Instance.HasEnoughLeads)
                {
                    // Voss stonewalls until the market corroborates his name
                    _activeDialogueList = new List<DialogueEntry>
                    {
                        new DialogueEntry { dialogueText = "Easy, blade. Word is you've been asking the whole market about me. Ask two people who'll actually say my name out loud, then we'll talk.", speakerMood = "Guarded" }
                    };
                }
                else if (state == BlackMarketStoryState.ReturnToVoss && returnDialogueEntries.Count > 0)
                {
                    _activeDialogueList = returnDialogueEntries;
                }
                else if (state == BlackMarketStoryState.InfiltrateRefineryTask)
                {
                    // Single waiting reminder
                    _activeDialogueList = new List<DialogueEntry>
                    {
                        new DialogueEntry { dialogueText = waitingDialogueText, speakerMood = "Impatient" }
                    };
                }
                else
                {
                    _activeDialogueList = dialogueEntries;
                }
            }
            else if (isStoryObjectiveNPC && SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance != null)
            {
                var mission = SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance;
                if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.Briefing)
                {
                    _activeDialogueList = dialogueEntries;
                }
                else if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.CollectingMatrices)
                {
                    int held = mission.tractorBeam != null ? mission.tractorBeam.HeldCoreCount : 0;
                    _activeDialogueList = new List<DialogueEntry>
                    {
                        new DialogueEntry
                        {
                            dialogueText = $"What are you waiting for, Alita? The 4 Core Matrices are still drifting outside the broken dome. Take the Nightshade Striker on the Western Flight Deck and engage your ventral tractor beam [T]! [{held}/4 Secured]",
                            speakerMood = "Impatient"
                        }
                    };
                }
                else if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.ReturnToCoreEngine ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.InsertingCores ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.ReactorIgnited ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.HyperspaceWarpUnlocked ||
                         (mission.tractorBeam != null && mission.tractorBeam.HeldCoreCount >= 4) ||
                         (CollectorBetrayalManager.Instance != null && (CollectorBetrayalManager.Instance.currentPhase == BetrayalPhase.WaitingForCoreReturn || CollectorBetrayalManager.Instance.currentPhase == BetrayalPhase.ConfrontingCollector)))
                {
                    if (CollectorBetrayalManager.Instance != null)
                    {
                        CollectorBetrayalManager.Instance.currentPhase = BetrayalPhase.ConfrontingCollector;
                    }

                    if (returnDialogueEntries != null && returnDialogueEntries.Count > 0)
                    {
                        _activeDialogueList = returnDialogueEntries;
                    }
                    else
                    {
                        _activeDialogueList = new List<DialogueEntry>
                        {
                            new DialogueEntry
                            {
                                dialogueText = "Alita: \"I brought all 4 antimatter cores from orbit, Voss. Every single matrix accounted for. Now hand over the Mars jump coordinates like you promised.\"",
                                speakerMood = "Neutral"
                            },
                            new DialogueEntry
                            {
                                dialogueText = "Voss: \"All four intact URM cores... Transferring them into my vault right now. You really came through, Alita. The Syndicate owes you a great debt.\"",
                                speakerMood = "Sinister"
                            },
                            new DialogueEntry
                            {
                                dialogueText = "Voss: \"Which is why it's a real shame I have to break our deal. See, Omni-Corp offered 100,000 credits for your intact Berserker chassis. And I don't leave money on the table.\"",
                                speakerMood = "Sinister"
                            },
                            new DialogueEntry
                            {
                                dialogueText = "Voss: \"Did you honestly think I was going to let you fly off to Mars? You're worth far more to me dead! Syndicate Enforcers, lock down the concourse! Roll out the Heavy Siege Tank from the garage and blast her to scrap metal!\"",
                                speakerMood = "Aggressive"
                            },
                            new DialogueEntry
                            {
                                dialogueText = "Alita: \"You cowardly traitor, Voss! You used me to retrieve your cores and sold me out to Omni-Corp! I'm taking these coordinates, and when I'm done with your tank, I'm coming back for you!\"",
                                speakerMood = "Defiant"
                            }
                        };
                    }
                }
                else
                {
                    _activeDialogueList = dialogueEntries;
                }
            }
            else
            {
                _activeDialogueList = dialogueEntries;
            }

            PlayRandomChatter();
            ShowCurrentDialogue();
        }

        private float _lastAdvanceTime = 0f;

        public void AdvanceConversation()
        {
            // Debounce guard to prevent rapid multi-triggers
            if (Time.unscaledTime - _lastAdvanceTime < 0.18f) return;
            _lastAdvanceTime = Time.unscaledTime;

            if (_activeDialogueList == null)
            {
                FinishConversation();
                return;
            }

            _currentDialogueIndex++;
            if (_currentDialogueIndex < _activeDialogueList.Count)
            {
                if (animator != null)
                {
                    if (HasParameter("Gesture")) animator.SetTrigger("Gesture");
                }

                PlayRandomChatter();
                ShowCurrentDialogue();
            }
            else
            {
                FinishConversation();
            }
        }

        private void ShowCurrentDialogue()
        {
            if (_activeDialogueList != null && _currentDialogueIndex >= 0 && _currentDialogueIndex < _activeDialogueList.Count)
            {
                var entry = _activeDialogueList[_currentDialogueIndex];
                var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
                hud.DisplayDialogue(this, entry.dialogueText, _currentDialogueIndex + 1, _activeDialogueList.Count);
            }
        }

        public void FinishConversation()
        {
            _isConversing = false;
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
            hud.HideDialogue();

            if (animator != null)
            {
                ApplyIdlePosture(defaultIdleID);
            }

            if (isStoryObjectiveNPC && BlackMarketStoryCombatDirector.Instance != null)
            {
                var state = BlackMarketStoryCombatDirector.Instance.currentState;

                if (state == BlackMarketStoryState.InvestigateMarket)
                {
                    if (BlackMarketStoryCombatDirector.Instance.HasEnoughLeads)
                    {
                        // Task assigned!
                        BlackMarketStoryCombatDirector.Instance.OnVossTaskAssigned();
                    }
                    else
                    {
                        hud.ShowMissionBanner("VOSS ISN'T TALKING YET // CROSS-REFERENCE TWO MARKET LEADS");
                    }
                }
                else if (state == BlackMarketStoryState.ReturnToVoss)
                {
                    // Double-cross revealed!
                    MarsCoordinatesUnlocked = true;
                    BlackMarketStoryCombatDirector.Instance.OnVossBetrayalRevealed();
                }
            }
            else if (isStoryObjectiveNPC && SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance != null)
            {
                var mission = SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance;
                if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.Briefing)
                {
                    mission.currentStage = SyntyCombined.World.CoreRetrievalStage.CollectingMatrices;

                    if (CyberpunkHUDController.Instance != null)
                    {
                        CyberpunkHUDController.Instance.SetQuestObjective(
                            "CORE RETRIEVAL",
                            "► Head to Flight Deck, Board Starship & Retrieve 4 Core Matrices [0/4]"
                        );
                    }

                    hud.ShowMissionBanner("MISSION ACCEPTED: BOARD STARSHIP ON FLIGHT DECK & RETRIEVE 4 CORES");
                    hud.ShowTransmission(
                        characterName.ToUpper(),
                        roleBadgeTitle,
                        "The SS-04 Nightshade Striker is prepped on Pad 02. Fly out through the broken dome and secure all 4 core matrices!",
                        badgeColor,
                        7.0f
                    );
                }
                else if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.CollectingMatrices && (mission.tractorBeam == null || mission.tractorBeam.HeldCoreCount < 4))
                {
                    int held = mission.tractorBeam != null ? mission.tractorBeam.HeldCoreCount : 0;
                    hud.ShowMissionBanner($"MISSION ACTIVE: RETRIEVE 4 ASTEROID CORE MATRICES [{held}/4]");
                }
                else if (mission.currentStage == SyntyCombined.World.CoreRetrievalStage.ReturnToCoreEngine ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.InsertingCores ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.HyperspaceWarpUnlocked ||
                         mission.currentStage == SyntyCombined.World.CoreRetrievalStage.ReactorIgnited ||
                         (mission.tractorBeam != null && mission.tractorBeam.HeldCoreCount >= 4) ||
                         (CollectorBetrayalManager.Instance != null && (CollectorBetrayalManager.Instance.currentPhase == BetrayalPhase.ConfrontingCollector || CollectorBetrayalManager.Instance.currentPhase == BetrayalPhase.WaitingForCoreReturn)))
                {
                    if (CollectorBetrayalManager.Instance != null)
                    {
                        CollectorBetrayalManager.Instance.OnCollectorBetrayalDialogueFinished();
                    }
                    else
                    {
                        MarsCoordinatesUnlocked = true;
                        hud.ShowMissionBanner("MARS COORDINATES UNLOCKED // FLEE THE SANCTUARY POD!");
                    }
                }
            }
            else if (isStoryObjectiveNPC)
            {
                MarsCoordinatesUnlocked = true;
                hud.ShowMissionBanner(unlockObjectiveText);
            }
            else if (BlackMarketStoryCombatDirector.Instance != null && !string.IsNullOrEmpty(leadId))
            {
                // Informant conversation cross-references a lead on Voss
                BlackMarketStoryCombatDirector.Instance.RegisterLead(leadId, leadTitle, leadInsight);
            }
        }

        public void CloseConversation()
        {
            _isConversing = false;
            var hud = BlackMarketNPCDialogueHUD.GetOrCreate();
            hud.HideDialogue();

            if (animator != null)
            {
                ApplyIdlePosture(defaultIdleID);
            }
        }

        private void PlayRandomChatter()
        {
            if (voiceChatterSFX != null && voiceChatterSFX.Length > 0)
            {
                var clip = voiceChatterSFX[Random.Range(0, voiceChatterSFX.Length)];
                if (clip != null)
                {
                    AudioSource.PlayClipAtPoint(clip, transform.position, 0.75f);
                }
            }
        }

        private void OnGUI()
        {
            if (!canTalk && !isStoryObjectiveNPC) return;
            if (!_isPlayerInRange || _isConversing) return;

            string action = "Talk to " + characterName;
            if (isStoryObjectiveNPC && BlackMarketStoryCombatDirector.Instance != null)
            {
                var state = BlackMarketStoryCombatDirector.Instance.currentState;
                if (state == BlackMarketStoryState.ReturnToVoss)
                {
                    action = "Deliver 4 Antimatter Cores to Voss";
                }
                else if (state == BlackMarketStoryState.InfiltrateRefineryTask)
                {
                    action = "Speak with Kaelen Voss";
                }
                else if (state == BlackMarketStoryState.InvestigateMarket && !BlackMarketStoryCombatDirector.Instance.HasEnoughLeads)
                {
                    action = "Confront Kaelen Voss";
                }
            }
            else if (isStoryObjectiveNPC && SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance != null)
            {
                var mission = SyntyCombined.World.BlackMarketCoreRetrievalMission.Instance;
                if (mission.tractorBeam != null && mission.tractorBeam.HeldCoreCount >= 4)
                {
                    action = "Deliver 4 Antimatter Cores to Voss";
                }
            }

            Cyberpunk.UI.CyberpunkInteractionPromptHUD.DrawPromptImmediate("E", action);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = badgeColor;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            Gizmos.DrawWireCube(transform.position + promptOffset, new Vector3(0.3f, 0.1f, 0.3f));
        }
    }
}
