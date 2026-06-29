using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Click/roll-triggered dice tumble. On <see cref="Spin"/> the cube does a fake-3D forward roll
    /// for <see cref="spinDuration"/> seconds: it rotates around its X axis (which foreshortens the
    /// flat UI quad like a flipping die "revealing its back faces"), swaps its pip face each quarter
    /// turn, fades translucent while fast (cheap motion-blur feel), and eases to a stop on a face.
    /// Triggered by <see cref="SlimeRoller.Roll"/> (so manual taps AND Auto Roll both animate).
    /// The permanent "Roll Speed" skill calls <see cref="SpeedUp"/> to shorten the tumble.
    /// </summary>
    public class DiceSpinner : MonoBehaviour
    {
        public RectTransform target;
        public float spinDuration = 2f;   // seconds for one click tumble
        public float minDuration = 0.5f;
        public int flips = 5;             // forward rolls per tumble
        public Image[] facePips;          // 7 pips: 0=TL 1=TR 2=ML 3=MR 4=BL 5=BR 6=C (rest face)
        public CanvasGroup blurGroup;     // faded while fast for a motion-blur feel

        [Header("Reel (slime faces + 2X flashing while spinning)")]
        public Image reelIcon;            // slime body shown during the spin (covers the hidden cube)
        public GameObject reelEyes;       // the slime's eyes (on for slime frames, off for the 2X frame)
        public Text reelText;             // "2X" shown on the 2X frames
        public SlimeRoller roller;        // source of the curated reel colours
        public Color[] reelColors;        // fallback list if roller is null

        Color _resultColor = Color.white; // the slime the reel slows down and lands on

        Coroutine _co;
        bool _spinning;

        // pip indices lit for faces 1..6
        static readonly int[][] Faces = {
            new int[]{6},                 // 1
            new int[]{0,5},               // 2
            new int[]{0,6,5},             // 3
            new int[]{0,1,4,5},           // 4
            new int[]{0,1,6,4,5},         // 5
            new int[]{0,1,2,3,4,5},       // 6
        };

        public void Spin(Color resultColor)
        {
            if (target == null || !isActiveAndEnabled) return;
            if (_spinning) return; // already tumbling — don't restart (auto-roll fired this every cooldown)
            _resultColor = resultColor;
            _co = StartCoroutine(Tumble());
        }

        IEnumerator Tumble()
        {
            _spinning = true;
            if (roller != null) { var rc = roller.GetReelColors(); if (rc != null && rc.Length > 0) reelColors = rc; }
            bool reel = reelIcon != null;
            if (reel) reelIcon.gameObject.SetActive(true);
            if (blurGroup != null) blurGroup.alpha = 0f;   // HIDE the cube — the reel slime takes over
            target.localRotation = Quaternion.identity;

            float dur = Mathf.Max(0.4f, spinDuration);
            float lockAt = dur - 0.5f;                      // last 0.5s: locked on the result so you see what you got
            float t = 0f, nextFlip = 0f; int reelIdx = 0; bool locked = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (reel)
                {
                    if (!locked && t >= lockAt) { locked = true; ShowReelSlime(_resultColor); }
                    else if (!locked && t >= nextFlip)
                    {
                        AdvanceReel(ref reelIdx);
                        float u = t / dur;
                        nextFlip = t + Mathf.Lerp(0.04f, 0.17f, u * u); // flashes fast then slows down
                    }
                }
                yield return null;
            }
            if (reel) ShowReelSlime(_resultColor);
            yield return null;

            if (reel) { reelIcon.gameObject.SetActive(false); if (reelText != null) reelText.gameObject.SetActive(false); }
            if (blurGroup != null) blurGroup.alpha = 1f;    // cube back
            SetFace(Random.Range(1, 7));
            _co = null;
            _spinning = false;
        }

        // Cycle the reel: next slime colour (with eyes), with a "2X" frame mixed in.
        void AdvanceReel(ref int idx)
        {
            if (reelColors == null || reelColors.Length == 0) return;
            int n = reelColors.Length;
            idx = (idx + 1) % (n + 1);          // last index = the 2X frame
            if (idx == n) SetReel(new Color(1f, 0.84f, 0.25f, 1f), true);
            else SetReel(reelColors[idx], false);
        }

        void ShowReelSlime(Color c) => SetReel(c, false);

        void SetReel(Color bodyColor, bool twoX)
        {
            if (reelIcon != null) reelIcon.color = bodyColor;
            if (reelEyes != null) reelEyes.SetActive(!twoX);
            if (reelText != null) reelText.gameObject.SetActive(twoX);
        }

        public void SetFace(int n)
        {
            if (facePips == null) return;
            n = Mathf.Clamp(n, 1, 6);
            var lit = Faces[n - 1];
            for (int i = 0; i < facePips.Length; i++)
            {
                if (facePips[i] == null) continue;
                facePips[i].gameObject.SetActive(System.Array.IndexOf(lit, i) >= 0);
            }
        }

        /// <summary>Multiply the tumble duration (e.g. 0.8 = 20% faster), clamped to minDuration.</summary>
        public void SpeedUp(float factor)
        {
            spinDuration = Mathf.Max(minDuration, spinDuration * factor);
        }
    }
}
