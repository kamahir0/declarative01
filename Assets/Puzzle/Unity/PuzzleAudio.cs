using System.Collections.Generic;
using UnityEngine;

namespace DeclarativePuzzle.Unity
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class PuzzleAudio : MonoBehaviour
    {
        readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
        AudioSource source;
        public void Play(int midiNote, float volume = .14f)
        {
            if (source == null) { source = GetComponent<AudioSource>(); source.playOnAwake = false; }
            if (!clips.TryGetValue(midiNote, out var clip))
            {
                const int sampleRate = 22050;
                var samples = new float[4410];
                var frequency = 440 * Mathf.Pow(2, (midiNote - 69) / 12f);
                for (var i = 0; i < samples.Length; i++)
                {
                    var time = i / (float)sampleRate;
                    var envelope = Mathf.Min(time * 180, 1) * Mathf.Pow(1 - i / (float)samples.Length, 3);
                    samples[i] = (Mathf.Sin(time * frequency * Mathf.PI * 2) + .18f * Mathf.Sin(time * frequency * Mathf.PI * 4)) * envelope * .65f;
                }
                clip = AudioClip.Create("PuzzleTone" + midiNote, samples.Length, 1, sampleRate, false);
                clip.SetData(samples, 0); clips.Add(midiNote, clip);
            }
            source.PlayOneShot(clip, volume);
        }
        void OnDestroy() { foreach (var clip in clips.Values) Destroy(clip); clips.Clear(); }
    }
}
