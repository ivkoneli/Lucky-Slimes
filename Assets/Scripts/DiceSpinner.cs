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
        public Image[] facePips;          // 7 pips: 0=TL 1=TR 2=ML 3=MR 4=BL 5=BR 6=C
        public CanvasGroup blurGroup;     // faded while fast for a motion-blur feel

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

        public void Spin()
        {
            if (target == null || !isActiveAndEnabled) return;
            if (_spinning) return; // already tumbling — don't restart (auto-roll fired this every cooldown)
            _co = StartCoroutine(Tumble());
        }

        IEnumerator Tumble()
        {
            _spinning = true;
            float dur = Mathf.Max(0.1f, spinDuration);
            float totalAng = 360f * Mathf.Max(1, flips);
            float t = 0f; int lastQuarter = -1;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur);
                float eased = 1f - Mathf.Pow(1f - u, 3f);          // ease-out: fast then settle
                float ang = totalAng * eased;
                target.localRotation = Quaternion.Euler(-ang, 0f, 0f); // tip top forward
                int q = Mathf.FloorToInt(ang / 90f);
                if (q != lastQuarter) { lastQuarter = q; SetFace(Random.Range(1, 7)); }
                if (blurGroup != null) blurGroup.alpha = Mathf.Lerp(0.5f, 1f, eased);
                yield return null;
            }
            target.localRotation = Quaternion.identity;
            if (blurGroup != null) blurGroup.alpha = 1f;
            SetFace(Random.Range(1, 7));
            _co = null;
            _spinning = false;
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
