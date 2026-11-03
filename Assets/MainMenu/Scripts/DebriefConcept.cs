using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The debrief on the safehouse wall, shown after a completed job from the save's lastJob (apartment-level.md
// 3.7): first THE TAKE (each item line slides in, the list scrolling to keep the newest in view, then the total
// counts up), then REPUTATION (where the XP came from, from XpRules, then the bar filling from the XP before the
// job to after it, each level worth a skill point). Continue skips a running animation or moves on. The tabs
// are open from the start: leaving a view mid-animation jumps it to its end first, and the first visit to
// REPUTATION plays it, so each view is always either playing or finished
public class DebriefConcept : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField, Tooltip("Menu that announces when the debrief station is shown")]
    private MainMenuConcept menu;

    [Header("Views")]
    [SerializeField, Tooltip("THE TAKE: stolen items and the total")]
    private CanvasGroup takeView;

    [SerializeField, Tooltip("REPUTATION: XP sources and the level bar")]
    private CanvasGroup repView;

    [SerializeField, Tooltip("Tab that shows THE TAKE")]
    private MenuButtonFx takeTab;

    [SerializeField, Tooltip("Tab that shows REPUTATION, playing it on the first visit")]
    private MenuButtonFx repTab;

    [SerializeField, Tooltip("Label of the Continue button: says what the next press does")]
    private TMP_Text continueLabel;

    [SerializeField, Tooltip("Title card's subtitle: the job, how it went, and its time")]
    private TMP_Text subtitle;

    [Header("The take")]
    [SerializeField, Tooltip("Scrolling list of item lines; 4 fit, the rest scroll")]
    private ScrollRect lootScroll;

    [SerializeField, Tooltip("Inactive row in the list, copied once per item line")]
    private DebriefRow rowTemplate;

    [SerializeField, Tooltip("Total line, revealed after the items")]
    private RectTransform takeTotalLine;

    [SerializeField, Tooltip("Total value text, counts up from 0")]
    private TMP_Text takeTotal;

    [Header("Reputation")]
    [SerializeField, Tooltip("XP source rows, one per XpRules line, revealed in this order")]
    private RectTransform[] repLines;

    [SerializeField, Tooltip("Bar fill. Its right anchor is set to the progress, 0 to 1")]
    private RectTransform barFill;

    [SerializeField, Tooltip("Level number text")]
    private TMP_Text levelText;

    [SerializeField, Tooltip("XP inside the level, as \"current / needed\"")]
    private TMP_Text xpText;

    [SerializeField, Tooltip("LEVEL UP banner, punched in each time the bar wraps")]
    private RectTransform levelUp;

    [Header("Timing")]
    [SerializeField, Tooltip("Seconds between two lines appearing")]
    private float lineInterval = 0.18f;

    [SerializeField, Tooltip("Seconds one line takes to slide and fade in")]
    private float lineIn = 0.35f;

    [SerializeField, Tooltip("Pixels a line slides in from the left")]
    private float lineSlide = 60f;

    [SerializeField, Tooltip("Seconds the bar takes to fill one whole level")]
    private float barPerLevel = 1.4f;

    private enum View { Take, Rep }

    private View view;
    private bool repPlayed, animating;
    private Coroutine routine;
    private float totalRest;
    private CanvasGroup levelUpGroup;
    private TMP_Text levelUpLabel;

    private readonly List<DebriefRow> takeRows = new();
    private DebriefRow[] repRows;
    private readonly List<XpLine> xpLines = new();

    // The job being shown, worked out from the save when the board opens
    private int total, gainedXp, startTotal, startLevel, startXp;

    private void Awake()
    {
        if (menu == null || takeView == null || repView == null || barFill == null || lootScroll == null || rowTemplate == null)
        {
            Debug.LogWarning($"{name} is missing its menu, views, bar, loot list or row template", this);
            enabled = false;
            return;
        }
        repRows = new DebriefRow[repLines.Length];
        for (int i = 0; i < repLines.Length; i++) repRows[i] = repLines[i].GetComponent<DebriefRow>();
        if (takeTotalLine != null) totalRest = takeTotalLine.anchoredPosition.x;
        if (levelUp != null)
        {
            levelUpGroup = levelUp.GetComponent<CanvasGroup>();
            levelUpLabel = levelUp.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnEnable()
    {
        if (menu != null) menu.StationShown += OnStationShown;
    }

    private void OnDisable()
    {
        if (menu != null) menu.StationShown -= OnStationShown;
    }

    private void OnStationShown(MenuStation station, float panelsIn)
    {
        if (station != MenuStation.Debrief) return;

        // Seen once is enough: cleared as soon as the board shows, so leaving early does not bring it back
        ProgressSave save = ProgressStore.Load();
        if (save.debriefPending)
        {
            save.debriefPending = false;
            ProgressStore.Save(save);
        }

        FillHeader(save.lastJob);
        FillTake(save.lastJob);
        FillRep(save);

        repPlayed = false;
        ShowView(View.Take);
        HideLine(takeTotalLine);
        takeTotal.text = Wallet.Money(0);
        Run(PlayTake(panelsIn));
    }

    // Connected to the Continue button: skips a running animation, else moves to the next step
    public void Continue()
    {
        if (animating)
        {
            Finish();
            return;
        }
        if (view == View.Take && !repPlayed)
        {
            StartRep();
            return;
        }
        menu.OpenMain();
    }

    // Connected to the tabs. Finish first, so the view being left is complete when the player comes back
    public void ShowTake()
    {
        if (view == View.Take) return;
        if (animating) Finish();
        ShowView(View.Take);
    }

    public void ShowRep()
    {
        if (view == View.Rep) return;
        if (animating) Finish();
        if (repPlayed) ShowView(View.Rep);
        else StartRep();
    }

    // Resets REPUTATION to the XP before the job and plays it from there
    private void StartRep()
    {
        ShowView(View.Rep);
        foreach (DebriefRow row in repRows) if (row != null) row.Hide();
        SetBar(startLevel, startXp);
        HideLine(levelUp);
        Run(PlayRep());
    }

    // "APARTMENT  |  CLEAN GETAWAY  |  01:14"
    private void FillHeader(JobResult job)
    {
        if (subtitle == null) return;
        string title = string.IsNullOrEmpty(job.job) ? "NO JOB YET" : job.job.ToUpperInvariant();
        string getaway = job.peakAlert >= JobAlertLevel.Alarm ? "ALARM RAISED" : "CLEAN GETAWAY";
        int seconds = Mathf.FloorToInt(job.seconds);
        subtitle.text = $"{title}  |  {getaway}  |  {seconds / 60:00}:{seconds % 60:00}";
    }

    // One row per loot line, copied from the template, all hidden until the reveal turns them on one by one
    private void FillTake(JobResult job)
    {
        foreach (DebriefRow old in takeRows)
        {
            if (old == null) continue;
            old.gameObject.SetActive(false); // out of the layout now; Destroy only happens at the end of the frame
            Destroy(old.gameObject);
        }
        takeRows.Clear();

        total = 0;
        foreach (JobLoot loot in job.loot)
        {
            DebriefRow row = Instantiate(rowTemplate, lootScroll.content);
            string item = loot.name.ToUpperInvariant();
            row.Set(loot.count > 1 ? $"{item}  x{loot.count}" : item, loot.objective ? "Objective" : "Bonus", "+" + Wallet.Money(loot.value));
            row.Hide();
            row.gameObject.SetActive(false);
            takeRows.Add(row);
            total += loot.value;
        }
        lootScroll.verticalNormalizedPosition = 1f;
    }

    // The XP lines from XpRules, and where the bar starts: the save already holds the XP after the job
    private void FillRep(ProgressSave save)
    {
        gainedXp = XpRules.Score(save.lastJob, xpLines);
        if (xpLines.Count != repRows.Length)
            Debug.LogWarning($"{name}: XpRules gives {xpLines.Count} lines but the board has {repRows.Length} rows", this);

        for (int i = 0; i < repRows.Length; i++)
        {
            if (repRows[i] == null) continue;
            bool used = i < xpLines.Count;
            repRows[i].gameObject.SetActive(used);
            if (used) repRows[i].Set(xpLines[i].label, xpLines[i].detail, $"+{xpLines[i].xp} XP");
        }

        startTotal = Mathf.Max(0, save.xp - gainedXp);
        startLevel = Reputation.LevelFor(startTotal, out startXp);
        int levelsGained = Reputation.LevelFor(startTotal + gainedXp, out _) - startLevel;
        if (levelUpLabel != null)
            levelUpLabel.text = levelsGained > 1 ? $"LEVEL UP  +{levelsGained} SKILL POINTS" : "LEVEL UP  +1 SKILL POINT";
    }

    private IEnumerator PlayTake(float delay)
    {
        animating = true;
        UpdateContinueLabel();
        yield return new WaitForSeconds(delay);
        foreach (DebriefRow row in takeRows)
        {
            RevealTakeRow(row);
            yield return new WaitForSeconds(lineInterval);
        }
        RevealLine(takeTotalLine, totalRest);
        yield return Tween.Custom(this, 0f, total, 0.8f, (debrief, value) => debrief.takeTotal.text = Wallet.Money(Mathf.RoundToInt(value)), Ease.OutCubic).ToYieldInstruction();
        EndTake();
    }

    // Turns the row on, makes the list's height include it at once, and scrolls so the newest row is in view.
    // Up to four rows fit, so the scroll has nothing to do until the fifth
    private void RevealTakeRow(DebriefRow row)
    {
        row.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(lootScroll.content);
        row.Reveal(lineSlide, lineIn);
        Tween.Custom(lootScroll, lootScroll.verticalNormalizedPosition, 0f, lineIn, (scroll, value) => scroll.verticalNormalizedPosition = value, Ease.OutCubic);
    }

    private IEnumerator PlayRep()
    {
        animating = true;
        UpdateContinueLabel();
        yield return new WaitForSeconds(lineIn);
        foreach (DebriefRow row in repRows)
        {
            if (row == null || !row.gameObject.activeSelf) continue;
            row.Reveal(lineSlide, lineIn);
            yield return new WaitForSeconds(lineInterval);
        }

        // Fill level by level: each wrap punches the LEVEL UP banner in
        int level = startLevel, xp = startXp, left = gainedXp;
        while (left > 0)
        {
            int needed = Reputation.XpForLevel(level);
            int step = Mathf.Min(left, needed - xp);
            int from = xp;
            float seconds = barPerLevel * step / needed;
            yield return Tween.Custom(this, from, from + step, seconds, (debrief, value) => debrief.SetBar(level, Mathf.RoundToInt(value)), Ease.Linear).ToYieldInstruction();
            xp += step;
            left -= step;
            if (xp >= needed)
            {
                level++;
                xp = 0;
                SetBar(level, 0);
                PunchLevelUp();
            }
        }
        EndRep();
    }

    // Jumps the running view to its end state
    private void Finish()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        Tween.StopAll(this);
        Tween.StopAll(lootScroll);
        if (view == View.Take)
        {
            foreach (DebriefRow row in takeRows)
            {
                row.gameObject.SetActive(true);
                row.ShowNow();
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(lootScroll.content);
            lootScroll.verticalNormalizedPosition = 0f;
            ShowLine(takeTotalLine);
            if (takeTotalLine != null) takeTotalLine.anchoredPosition = new Vector2(totalRest, takeTotalLine.anchoredPosition.y);
            EndTake();
        }
        else
        {
            foreach (DebriefRow row in repRows) if (row != null) row.ShowNow();
            EndRep();
        }
    }

    private void EndTake()
    {
        takeTotal.text = Wallet.Money(total);
        animating = false;
        routine = null;
        UpdateContinueLabel();
    }

    private void EndRep()
    {
        int level = Reputation.LevelFor(startTotal + gainedXp, out int xp);
        SetBar(level, xp);
        if (level > startLevel) ShowLine(levelUp);
        repPlayed = true;
        animating = false;
        routine = null;
        UpdateContinueLabel();
    }

    private void SetBar(int level, int xp)
    {
        int needed = Reputation.XpForLevel(level);
        barFill.anchorMax = new Vector2(Mathf.Clamp01((float)xp / needed), barFill.anchorMax.y);
        if (levelText != null) levelText.text = level.ToString();
        if (xpText != null) xpText.text = $"{xp} / {needed} XP";
    }

    private void PunchLevelUp()
    {
        if (levelUp == null) return;
        if (levelUpGroup != null) Tween.Alpha(levelUpGroup, 0f, 1f, 0.15f);
        Tween.Scale(levelUp, 1.4f, 1f, 0.45f, Ease.OutBack);
    }

    private void ShowView(View next)
    {
        view = next;
        SetGroup(takeView, next == View.Take);
        SetGroup(repView, next == View.Rep);
        if (takeTab != null) takeTab.SetSelected(next == View.Take);
        if (repTab != null) repTab.SetSelected(next == View.Rep);
        UpdateContinueLabel();
    }

    private void UpdateContinueLabel()
    {
        if (continueLabel == null) return;
        continueLabel.text = animating ? "SKIP" : view == View.Take && !repPlayed ? "REPUTATION" : "DONE";
    }

    private void Run(IEnumerator sequence)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(sequence);
    }

    // The total line slides in like a row; it has no layout group above it, so it moves itself
    private void RevealLine(RectTransform line, float restX)
    {
        if (line == null) return;
        CanvasGroup group = line.GetComponent<CanvasGroup>();
        if (group != null) Tween.Alpha(group, 0f, 1f, lineIn * 0.6f);
        Tween.UIAnchoredPositionX(line, restX - lineSlide, restX, lineIn, Ease.OutBack);
    }

    private static void HideLine(RectTransform line)
    {
        if (line == null) return;
        CanvasGroup group = line.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;
    }

    // Stops the line's own tweens first, so a skipped reveal cannot move it after it has been placed
    private static void ShowLine(RectTransform line)
    {
        if (line == null) return;
        Tween.StopAll(line);
        line.localScale = Vector3.one;
        CanvasGroup group = line.GetComponent<CanvasGroup>();
        if (group == null) return;
        Tween.StopAll(group);
        group.alpha = 1f;
    }

    private static void SetGroup(CanvasGroup group, bool on)
    {
        group.alpha = on ? 1f : 0f;
        group.interactable = group.blocksRaycasts = on;
    }
}
