using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alita
{
    [Serializable]
    public class DialogueLine
    {
        public string speakerName = "DR. DYSON IDO";
        public string speakerTitle = "HUNTER-WARRIOR & CYBER-SURGEON";
        [TextArea(2, 5)]
        public string text = "";
        public Sprite customPortrait;
        public AudioClip voiceClip;
        public string animTrigger = "";

        public DialogueLine() { }

        public DialogueLine(string speaker, string title, string textContent, string anim = "")
        {
            speakerName = speaker;
            speakerTitle = title;
            text = textContent;
            animTrigger = anim;
        }
    }

    [Serializable]
    public class PrologueDialogueBeat
    {
        public string beatTitle = "";
        public List<DialogueLine> dialogueLines = new List<DialogueLine>();

        // Legacy / fallback accessors
        public string speakerName
        {
            get => dialogueLines.Count > 0 ? dialogueLines[0].speakerName : "DR. DYSON IDO";
            set
            {
                if (dialogueLines.Count == 0) dialogueLines.Add(new DialogueLine());
                dialogueLines[0].speakerName = value;
            }
        }

        public string speakerTitle
        {
            get => dialogueLines.Count > 0 ? dialogueLines[0].speakerTitle : "";
            set
            {
                if (dialogueLines.Count == 0) dialogueLines.Add(new DialogueLine());
                dialogueLines[0].speakerTitle = value;
            }
        }

        public string dialogueText
        {
            get => dialogueLines.Count > 0 ? dialogueLines[0].text : "";
            set
            {
                if (dialogueLines.Count == 0) dialogueLines.Add(new DialogueLine());
                dialogueLines[0].text = value;
            }
        }

        public AudioClip voiceClip
        {
            get => dialogueLines.Count > 0 ? dialogueLines[0].voiceClip : null;
            set
            {
                if (dialogueLines.Count == 0) dialogueLines.Add(new DialogueLine());
                dialogueLines[0].voiceClip = value;
            }
        }

        public string idoAnimTrigger = "";
        public int gestureId = 0;

        [Header("Interactive Look-At Landmark")]
        public bool hasInteractiveLookAt = false;
        public string lookAtPromptText = ""; // e.g. "[E] Gaze Up at Skyline"
        public Vector3 lookAtWorldTarget = Vector3.zero;
        public float lookAtFov = 35f;
        public float lookAtDuration = 4.0f;

        [Header("Waypoint Geometry")]
        public Vector3 waypointPosition = Vector3.zero;
        public float stopDistance = 3.5f;
        public bool requiresPlayerNearbyToSpeak = true;
    }
}
