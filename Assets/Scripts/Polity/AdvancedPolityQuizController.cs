using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudyMode.Polity
{
    /// <summary>
    /// A self-contained advanced Indian polity practice mode. The scene builder creates
    /// the UI; this component owns the question bank, scoring, timing and review flow.
    /// </summary>
    public sealed class AdvancedPolityQuizController : MonoBehaviour
    {
        private const int QuestionsPerRun = 15;
        private const float SecondsPerQuestion = 75f;

        private readonly List<Question> bank = new List<Question>();
        private readonly List<Question> run = new List<Question>();
        private readonly List<Attempt> attempts = new List<Attempt>();

        private TMP_FontAsset font;
        private TextMeshProUGUI progressText, scoreText, timerText, categoryText, questionText, feedbackText, actionText;
        private readonly List<Button> optionButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> optionLabels = new List<TextMeshProUGUI>();
        private Button nextButton, restartButton;
        private GameObject quizPanel, resultPanel;
        private int currentIndex;
        private int score;
        private float remaining;
        private bool answered;

        private readonly Color ink = new Color(0.85f, 0.91f, 0.98f);
        private readonly Color muted = new Color(0.46f, 0.56f, 0.68f);
        private readonly Color panel = new Color(0.035f, 0.055f, 0.095f, 0.98f);
        private readonly Color option = new Color(0.075f, 0.12f, 0.19f, 1f);
        private readonly Color cyan = new Color(0.10f, 0.83f, 1f, 1f);
        private readonly Color correct = new Color(0.16f, 0.72f, 0.44f, 1f);
        private readonly Color wrong = new Color(0.90f, 0.28f, 0.32f, 1f);

        private void Awake()
        {
            BuildQuestionBank();
            BuildInterface();
            StartNewRun();
        }

        private void Update()
        {
            if (answered || currentIndex >= run.Count) return;
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            timerText.text = string.Format("TIME  {0:00}:{1:00}", Mathf.FloorToInt(remaining / 60f), Mathf.CeilToInt(remaining % 60f));
            timerText.color = remaining <= 15f ? wrong : (remaining <= 30f ? new Color(1f, 0.72f, 0.2f) : cyan);
            if (remaining <= 0f) SelectAnswer(-1);
        }

        private void StartNewRun()
        {
            run.Clear();
            attempts.Clear();
            var pool = new List<Question>(bank);
            Shuffle(pool);
            run.AddRange(pool.GetRange(0, Mathf.Min(QuestionsPerRun, pool.Count)));
            currentIndex = 0;
            score = 0;
            resultPanel.SetActive(false);
            quizPanel.SetActive(true);
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            answered = false;
            remaining = SecondsPerQuestion;
            var q = run[currentIndex];
            progressText.text = string.Format("QUESTION {0:00} / {1:00}", currentIndex + 1, run.Count);
            scoreText.text = string.Format("SCORE  {0} / {1}", score, currentIndex);
            categoryText.text = q.topic.ToUpperInvariant() + "  •  VERY HARD";
            questionText.text = q.prompt;
            feedbackText.text = "<color=#8AA0B8>Select the most precise constitutional position. +1 correct · no negative marking.</color>";
            actionText.text = "";
            nextButton.gameObject.SetActive(false);
            for (int i = 0; i < optionButtons.Count; i++)
            {
                int optionIndex = i;
                var image = optionButtons[i].GetComponent<Image>();
                image.color = option;
                optionButtons[i].interactable = true;
                optionLabels[i].text = string.Format("<color=#45D8FF>{0}.</color>  {1}", (char)('A' + i), q.options[i]);
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => SelectAnswer(optionIndex));
            }
        }

        private void SelectAnswer(int selected)
        {
            if (answered) return;
            answered = true;
            var q = run[currentIndex];
            bool isCorrect = selected == q.answer;
            if (isCorrect) score++;
            attempts.Add(new Attempt { question = q, selected = selected, correct = isCorrect });
            for (int i = 0; i < optionButtons.Count; i++)
            {
                optionButtons[i].interactable = false;
                var image = optionButtons[i].GetComponent<Image>();
                if (i == q.answer) image.color = correct;
                else if (i == selected) image.color = wrong;
            }
            string chosen = selected < 0 ? "Time expired" : "Your answer: " + (char)('A' + selected);
            feedbackText.text = string.Format("<b><color=#{0}>{1}</color></b>  {2}\n\n<color=#D7E5F5>{3}</color>", isCorrect ? "48DC8A" : "FF6570", isCorrect ? "CORRECT" : "NOT QUITE", chosen, q.explanation);
            actionText.text = currentIndex == run.Count - 1 ? "Finish assessment" : "Continue";
            nextButton.gameObject.SetActive(true);
            scoreText.text = string.Format("SCORE  {0} / {1}", score, currentIndex + 1);
        }

        private void Next()
        {
            currentIndex++;
            if (currentIndex >= run.Count) ShowResults();
            else ShowQuestion();
        }

        private void ShowResults()
        {
            quizPanel.SetActive(false);
            resultPanel.SetActive(true);
            float percentage = 100f * score / run.Count;
            var resultTitle = resultPanel.transform.Find("ResultTitle").GetComponent<TextMeshProUGUI>();
            var resultBody = resultPanel.transform.Find("ResultBody").GetComponent<TextMeshProUGUI>();
            resultTitle.text = string.Format("{0} / {1}", score, run.Count);
            string band = percentage >= 80 ? "Constitutional specialist" : percentage >= 60 ? "Strong advanced foundation" : percentage >= 40 ? "Build case-law precision" : "Revisit constitutional architecture";
            var misses = new List<string>();
            foreach (var attempt in attempts)
                if (!attempt.correct) misses.Add("• " + attempt.question.topic + ": " + attempt.question.shortTitle);
            resultBody.text = string.Format("<color=#45D8FF>{0:0}%  •  {1}</color>\n\n<color=#D7E5F5>{2}</color>{3}", percentage, band, misses.Count == 0 ? "Perfect run. You handled the full high-difficulty set." : "Review focus:\n" + string.Join("\n", misses), misses.Count == 0 ? "" : "\n\nEach explanation is shown immediately after your response—use a new run for a fresh mixed set.");
        }

        private void BuildInterface()
        {
            font = TMP_Settings.defaultFontAsset;
            var canvasGo = new GameObject("Polity Quiz Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }

            var background = CreatePanel(canvasGo.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Color(0.012f, 0.02f, 0.045f, 1f));
            Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero);
            quizPanel = CreatePanel(canvasGo.transform, "QuizPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, panel);
            quizPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(1440, 880);
            AddFrame(quizPanel.transform);

            var eyebrow = CreateText(quizPanel.transform, "Eyebrow", "INDIAN POLITY  /  ADVANCED ASSESSMENT", 18, cyan, TextAlignmentOptions.Left);
            Place(eyebrow.rectTransform, new Vector2(58, -52), new Vector2(850, 34), new Vector2(0, 1), new Vector2(0, 1));
            var title = CreateText(quizPanel.transform, "Title", "CONSTITUTIONAL GAUNTLET", 41, ink, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, new Vector2(58, -103), new Vector2(1000, 58), new Vector2(0, 1), new Vector2(0, 1));
            progressText = CreateText(quizPanel.transform, "Progress", "QUESTION 01 / 15", 18, muted, TextAlignmentOptions.Right);
            Place(progressText.rectTransform, new Vector2(-58, -62), new Vector2(320, 32), new Vector2(1, 1), new Vector2(1, 1));
            scoreText = CreateText(quizPanel.transform, "Score", "SCORE  0 / 0", 18, cyan, TextAlignmentOptions.Right);
            Place(scoreText.rectTransform, new Vector2(-58, -101), new Vector2(320, 32), new Vector2(1, 1), new Vector2(1, 1));
            AddLine(quizPanel.transform, new Vector2(58, -145), new Vector2(1324, 2), new Color(.12f, .36f, .55f, .8f), new Vector2(0, 1));

            categoryText = CreateText(quizPanel.transform, "Category", "PARLIAMENT  •  VERY HARD", 16, new Color(1f, .72f, .24f), TextAlignmentOptions.Left);
            Place(categoryText.rectTransform, new Vector2(58, -188), new Vector2(700, 26), new Vector2(0, 1), new Vector2(0, 1));
            timerText = CreateText(quizPanel.transform, "Timer", "TIME  01:15", 20, cyan, TextAlignmentOptions.Right);
            timerText.fontStyle = FontStyles.Bold;
            Place(timerText.rectTransform, new Vector2(-58, -184), new Vector2(220, 30), new Vector2(1, 1), new Vector2(1, 1));
            questionText = CreateText(quizPanel.transform, "Question", "", 29, ink, TextAlignmentOptions.TopLeft);
            questionText.textWrappingMode = TextWrappingModes.Normal;
            Place(questionText.rectTransform, new Vector2(58, -230), new Vector2(1324, 154), new Vector2(0, 1), new Vector2(0, 1));

            for (int i = 0; i < 4; i++)
            {
                var button = CreateButton(quizPanel.transform, "Option_" + i, new Vector2(642, 88));
                Place(button.GetComponent<RectTransform>(), new Vector2(58 + (i % 2) * 682, -408 - (i / 2) * 108), new Vector2(642, 88), new Vector2(0, 1), new Vector2(0, 1));
                optionButtons.Add(button);
                var label = CreateText(button.transform, "Label", "", 18, ink, TextAlignmentOptions.MidlineLeft);
                label.textWrappingMode = TextWrappingModes.Normal;
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 0));
                optionLabels.Add(label);
            }
            feedbackText = CreateText(quizPanel.transform, "Feedback", "", 18, ink, TextAlignmentOptions.TopLeft);
            feedbackText.textWrappingMode = TextWrappingModes.Normal;
            Place(feedbackText.rectTransform, new Vector2(58, -650), new Vector2(1324, 125), new Vector2(0, 1), new Vector2(0, 1));
            nextButton = CreateButton(quizPanel.transform, "NextButton", new Vector2(260, 58), cyan);
            Place(nextButton.GetComponent<RectTransform>(), new Vector2(-58, 48), new Vector2(260, 58), new Vector2(1, 0), new Vector2(1, 0));
            actionText = CreateText(nextButton.transform, "Label", "Continue", 18, new Color(.01f, .04f, .08f), TextAlignmentOptions.Center);
            actionText.fontStyle = FontStyles.Bold;
            Stretch(actionText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            nextButton.onClick.AddListener(Next);

            resultPanel = CreatePanel(canvasGo.transform, "ResultPanel", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, panel);
            resultPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 670);
            AddFrame(resultPanel.transform);
            var resultEyebrow = CreateText(resultPanel.transform, "ResultEyebrow", "ASSESSMENT COMPLETE", 18, cyan, TextAlignmentOptions.Center);
            Place(resultEyebrow.rectTransform, new Vector2(0, -85), new Vector2(700, 32), new Vector2(.5f, 1), new Vector2(.5f, 1));
            var resultTitle = CreateText(resultPanel.transform, "ResultTitle", "0 / 15", 76, ink, TextAlignmentOptions.Center);
            resultTitle.fontStyle = FontStyles.Bold;
            Place(resultTitle.rectTransform, new Vector2(0, -175), new Vector2(700, 95), new Vector2(.5f, 1), new Vector2(.5f, 1));
            var resultBody = CreateText(resultPanel.transform, "ResultBody", "", 20, ink, TextAlignmentOptions.TopLeft);
            resultBody.textWrappingMode = TextWrappingModes.Normal;
            Place(resultBody.rectTransform, new Vector2(0, -285), new Vector2(700, 230), new Vector2(.5f, 1), new Vector2(.5f, 1));
            restartButton = CreateButton(resultPanel.transform, "RestartButton", new Vector2(310, 62), cyan);
            Place(restartButton.GetComponent<RectTransform>(), new Vector2(0, 55), new Vector2(310, 62), new Vector2(.5f, 0), new Vector2(.5f, 0));
            var restartLabel = CreateText(restartButton.transform, "Label", "NEW MIXED RUN", 18, new Color(.01f, .04f, .08f), TextAlignmentOptions.Center);
            restartLabel.fontStyle = FontStyles.Bold;
            Stretch(restartLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero);
            restartButton.onClick.AddListener(StartNewRun);
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.anchoredPosition = position;
            go.GetComponent<Image>().color = color;
            return go;
        }
        private Button CreateButton(Transform parent, string name, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false); go.GetComponent<RectTransform>().sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color ?? option;
            var button = go.GetComponent<Button>(); var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(.72f, .94f, 1f); colors.pressedColor = new Color(.35f, .78f, .94f); colors.selectedColor = Color.white; button.colors = colors;
            return button;
        }
        private TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment; label.raycastTarget = false;
            return label;
        }
        private static void Place(RectTransform rect, Vector2 pos, Vector2 size, Vector2 anchor, Vector2 pivot) { rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = pos; rect.sizeDelta = size; }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 padding) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = new Vector2(padding.x, padding.y); rect.offsetMax = new Vector2(-padding.x, -padding.y); }
        private void AddLine(Transform parent, Vector2 pos, Vector2 size, Color color, Vector2 anchor) { var line = CreatePanel(parent, "Line", anchor, anchor, pos, color); line.GetComponent<RectTransform>().sizeDelta = size; }
        private void AddFrame(Transform parent) { AddLine(parent, Vector2.zero, new Vector2(1, 1), new Color(.14f, .65f, .9f, .75f), new Vector2(0, 0)); }
        private static void Shuffle<T>(List<T> items) { for (int i = items.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); T t = items[i]; items[i] = items[j]; items[j] = t; } }

        private void BuildQuestionBank()
        {
            Add("Emergency Provisions", "A proclamation under Article 352 is issued after the 44th Amendment. Which combination is constitutionally indispensable for its continuance beyond one month?", 1,
                "Approval by both Houses by a simple majority.", "Written Union Cabinet advice before proclamation, then approval by each House by majority of total membership and two-thirds of members present and voting.", "Prior consent of the Chief Justice of India and approval by Lok Sabha alone.", "Approval by a joint sitting by two-thirds of total membership.",
                "Article 352 requires a written Cabinet decision before issue. Each House must then approve within one month by the special majority: majority of total membership plus two-thirds present and voting.");
            Add("Federalism", "Parliament legislates on a State List subject because Rajya Sabha passes an Article 249 resolution. What majority is required in Rajya Sabha?", 2,
                "Majority of total membership.", "Two-thirds of total membership.", "Not less than two-thirds of members present and voting.", "Simple majority of members present and voting.",
                "Article 249 uses the phrase ‘not less than two-thirds of the members present and voting’ in Rajya Sabha; it is not a majority of the total membership.");
            Add("Parliament", "Which statement most accurately distinguishes a Financial Bill under Article 117(1) from a Money Bill?", 3,
                "Both may only be introduced in Lok Sabha, and Rajya Sabha can only recommend changes to both.", "A Financial Bill may be introduced in either House with the President’s recommendation.", "A Financial Bill cannot be amended by Rajya Sabha once Lok Sabha has passed it.", "A Financial Bill under Article 117(1) is introduced only in Lok Sabha on presidential recommendation, but Rajya Sabha has full legislative power over it and a joint sitting is possible.",
                "A Category I Financial Bill contains Article 110 matters plus other matters. It has Lok Sabha introduction/presidential recommendation constraints, but it is not a Money Bill; Rajya Sabha may amend or reject it and Article 108 can apply.");
            Add("Judicial Review", "In I.R. Coelho v. State of Tamil Nadu, the Supreme Court held that Ninth Schedule laws placed there after which date are open to basic-structure review?", 0,
                "24 April 1973.", "26 November 1949.", "18 July 1973.", "1 March 1977.",
                "The controlling date is 24 April 1973, the date of Kesavananda Bharati. Post-date Ninth Schedule insertions may be examined if they damage basic-structure principles.");
            Add("Tenth Schedule", "After Kihoto Hollohan, which proposition best states the position regarding a Speaker’s decision on disqualification?", 1,
                "It is wholly immune from judicial review because it is a proceeding of the House.", "It is subject to judicial review after the decision, but ordinarily not at an interlocutory stage except on jurisdictional or constitutional-malafide grounds.", "It is appealable only to the Election Commission.", "It may be reviewed only after the next general election.",
                "Kihoto Hollohan upheld the Schedule while allowing judicial review after a decision. Premature interference is exceptional, such as jurisdictional error or constitutional mala fides.");
            Add("Ordinances", "An ordinance promulgated under Article 123 will cease to operate six weeks after reassembly unless earlier disapproved. If the two Houses reassemble on different dates, the six-week period runs from:", 2,
                "The earlier date of reassembly.", "The date Lok Sabha reassembles.", "The later date of reassembly.", "The date the President addresses Parliament.",
                "Article 123 expressly measures the six weeks from the reassembly of Parliament; where Houses reassemble on different dates, it means the later date.");
            Add("Governor", "Under Article 200, which bill cannot be returned by the Governor to the State Legislature for reconsideration?", 1,
                "An ordinary bill.", "A Money Bill.", "A bill reserved for the President after the President directs reconsideration.", "A bill passed in a joint sitting of a State Legislature.",
                "The proviso to Article 200 permits return of a bill only ‘if it is not a Money Bill’. The Governor may assent, withhold assent, reserve, or return an eligible bill.");
            Add("GST", "A GST Council decision needs a weighted three-fourths majority of votes cast. What is the weight of the Union Government’s vote?", 0,
                "One-third of the total votes cast.", "One-half of the total votes cast.", "Two-thirds of the total votes cast.", "One-fourth of the total votes cast.",
                "Under Article 279A, the Centre has one-third weight and all State governments collectively have two-thirds. The threshold is at least three-fourths of weighted votes cast.");
            Add("All India Services", "A new All India Service common to the Union and States may be created by Parliament only after Rajya Sabha declares it necessary in national interest under Article 312. That declaration requires:", 2,
                "A majority of total membership of Rajya Sabha.", "A majority of total membership of Parliament sitting jointly.", "Not less than two-thirds of members of Rajya Sabha present and voting.", "Consent of every State Legislature.",
                "Article 312 mirrors Article 249’s special Rajya Sabha resolution: two-thirds of members present and voting, followed by a parliamentary law.");
            Add("Constitutional Bodies", "The Comptroller and Auditor-General may be removed from office in the same manner and on the same grounds as a Supreme Court judge. Which is correct?", 3,
                "By the President after a simple-majority resolution of Lok Sabha.", "By Parliament through a joint sitting by two-thirds of those present and voting.", "By the President on the advice of the Union Cabinet alone.", "By the President after an address by each House supported by majority of total membership and two-thirds present and voting, on proved misbehaviour or incapacity.",
                "Article 148 gives the CAG the same removal safeguard as a Supreme Court judge: a special-majority address in each House for proved misbehaviour or incapacity.");
            Add("Local Government", "For a District Planning Committee under Article 243ZD, at least what proportion of its members must be elected by and from elected members of the district panchayat and municipalities?", 2,
                "One-half.", "Two-thirds.", "Four-fifths.", "Three-fourths.",
                "At least four-fifths must be elected by and from elected members of district panchayats and municipalities, in proportion to rural and urban population.");
            Add("Writ Jurisdiction", "Which statement about Articles 32 and 226 is most accurate?", 1,
                "Article 32 is wider because it can enforce fundamental and all other legal rights.", "Article 226 is wider in subject matter because High Courts may issue writs for fundamental rights and ‘for any other purpose’.", "Both jurisdictions are identical, except that Article 32 applies in emergencies.", "Article 226 can be invoked only after exhausting Article 32.",
                "Article 32 is itself a fundamental right, but it is confined to enforcement of fundamental rights. Article 226 is broader in reach, covering other legal rights as well.");
            Add("President", "A resolution for impeachment of the President under Article 61 must be moved after 14 days’ written notice signed by at least one-fourth of the total membership of the House. The resolution itself must pass by:", 0,
                "A majority of not less than two-thirds of the total membership of that House.", "Two-thirds of members present and voting.", "A simple majority of total membership.", "A majority of total membership plus two-thirds present and voting.",
                "Article 61 is stricter than the usual special majority: the originating House must pass the charge by a majority of not less than two-thirds of its total membership.");
            Add("Amendment Power", "Which feature specifically follows from Article 368(2) after the 24th Amendment?", 3,
                "A constitutional amendment may be initiated in a State Legislature.", "Rajya Sabha has no role in a constitutional amendment bill.", "A joint sitting may resolve a deadlock over an amendment bill.", "The President shall give assent to a duly passed constitutional amendment bill.",
                "Article 368(2) uses ‘shall give his assent’. A constitutional amendment bill may originate in either House, has no joint sitting, and may require State ratification for specified matters.");
            Add("President’s Rule", "After one year of President’s Rule in a State, every further continuation up to the constitutional maximum requires which additional conditions?", 1,
                "Only a further six-month approval by Parliament.", "A National Emergency must be in operation in the whole or any part of India, and the Election Commission must certify difficulty in holding Assembly elections.", "The concerned State Legislature must give its consent.", "The Supreme Court must approve continuation.",
                "Article 356 ordinarily caps the period at one year. Extension up to three years needs parliamentary approval every six months plus both the Article 352 emergency and Election Commission certification conditions.");
            Add("Repugnancy", "A State law on a Concurrent List matter conflicts with a prior parliamentary law. It has received the President’s assent under Article 254(2). What is the best statement?", 1,
                "The State law is void throughout India.", "It prevails in that State, but Parliament can later override it by enacting a law on the same matter.", "It permanently immunises the State law from later parliamentary legislation.", "It prevails only if every other State ratifies it.",
                "Presidential assent saves the inconsistent State law within that State. Parliament retains power to add to, amend, vary or repeal it later.");
            Add("Parliamentary Committees", "Which composition correctly describes the Public Accounts Committee?", 0,
                "22 members: 15 from Lok Sabha and 7 from Rajya Sabha; ministers cannot be members.", "30 Lok Sabha members only, elected by proportional representation.", "22 members: 7 from Lok Sabha and 15 from Rajya Sabha.", "A committee chaired ex officio by the Comptroller and Auditor-General.",
                "The PAC has 22 members—15 Lok Sabha and 7 Rajya Sabha—elected annually by proportional representation. A minister cannot be a member; the CAG assists, but does not chair it.");
            Add("Basic Structure", "Which pairing is correctly matched with the doctrine’s development?", 2,
                "Golak Nath — Parliament can amend every part of the Constitution without limitation.", "Minerva Mills — Article 31C as extended to all Directive Principles was fully upheld.", "Kesavananda Bharati — Parliament may amend any part, but cannot damage or destroy the Constitution’s basic structure.", "Waman Rao — all Ninth Schedule laws are immune from review irrespective of date.",
                "Kesavananda Bharati formulated the basic-structure limitation. Minerva Mills struck down the 42nd Amendment’s expansion of Article 31C; Waman Rao established the 24 April 1973 cut-off framework.");
            Add("Emergency Provisions", "During a National Emergency, one-tenth of Lok Sabha’s total membership gives written notice seeking a special sitting to consider disapproval. The President must convene the sitting within:", 3,
                "Seven days.", "Ten days.", "Twenty-one days.", "Fourteen days.",
                "Article 352 provides this special Lok Sabha safeguard: on notice by not less than one-tenth of total members, a special sitting must be held within fourteen days.");
            Add("Election Law", "Which body decides questions of disqualification of a Member of Parliament under Article 103, after obtaining the Election Commission’s opinion?", 1,
                "The Speaker or Chairman of the relevant House.", "The President.", "The Supreme Court directly under Article 136.", "A joint committee of Parliament.",
                "For MPs, Article 103 assigns the decision to the President, who must obtain the Election Commission’s opinion and act according to it. Article 192 provides the parallel Governor route for State legislators.");
            Add("Fundamental Rights", "A challenge alleging that a law violates Article 14 may be brought directly under Article 32. Which statement captures the scope of the Supreme Court’s power there?", 0,
                "It may issue directions, orders, or writs—including habeas corpus, mandamus, prohibition, quo warranto and certiorari—for enforcement of fundamental rights.", "It can issue only habeas corpus and certiorari.", "It can enforce Directive Principles but not Fundamental Rights.", "It is discretionary in the same sense as Article 226 and may refuse every petition for alternate remedy.",
                "Article 32(2) expressly gives the Supreme Court power to issue directions, orders and the listed writs for enforcement of Part III rights. The right to move the Court is guaranteed by Article 32(1).");
            Add("Constitutional Bodies", "Which of the following is a recommendation of the Finance Commission under Article 280, rather than a binding executive order?", 2,
                "The annual Union Budget.", "The appointment of the CAG.", "The distribution between Union and States of net proceeds of taxes and allocation among States.", "The conduct of elections to Parliament.",
                "The Finance Commission recommends tax devolution, grants-in-aid principles and measures to augment State Consolidated Funds for local bodies. Its recommendations guide fiscal federalism but are not self-executing commands.");
            Add("Judiciary", "Which statement about a Supreme Court judge’s removal is correct?", 3,
                "The Chief Justice of India may remove a judge after an in-house inquiry.", "A joint sitting of Parliament may remove a judge by ordinary majority.", "The President may remove a judge on Cabinet advice without Parliament.", "Each House must separately present an address in the same session, supported by majority of total membership and two-thirds of members present and voting, for proved misbehaviour or incapacity.",
                "Article 124(4) requires separate special-majority addresses by both Houses in the same session. The President’s removal order follows that parliamentary address.");
            Add("Directive Principles", "Article 39(b) seeks that ownership and control of material resources be distributed to subserve the common good. Following the Supreme Court’s 2024 interpretation, which is the most accurate proposition?", 1,
                "Every privately owned resource automatically becomes a ‘material resource of the community’.", "Private resources may in some circumstances fall within the phrase, but the phrase does not automatically include all privately owned resources.", "Article 39(b) applies only to Union Government property.", "Article 39(b) is enforceable directly as a Fundamental Right.",
                "The Court rejected an automatic equation of all private property with community material resources. Article 39(b) remains a Directive Principle, informing policy rather than creating a directly enforceable right.");
        }

        private void Add(string topic, string prompt, int answer, string a, string b, string c, string d, string explanation)
        {
            bank.Add(new Question { topic = topic, prompt = prompt, answer = answer, options = new[] { a, b, c, d }, explanation = explanation, shortTitle = prompt.Length > 68 ? prompt.Substring(0, 65) + "…" : prompt });
        }
        private sealed class Question { public string topic, prompt, explanation, shortTitle; public int answer; public string[] options; }
        private sealed class Attempt { public Question question; public int selected; public bool correct; }
    }
}
