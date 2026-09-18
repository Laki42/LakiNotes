using System;
using System.Collections.Generic;
using System.Reflection;

namespace Laki
{
    internal sealed class LakiSelector
    {
        private readonly Type customNote;
        private readonly Type customBeatmap;
        private readonly PropertyInfo customData;
        public LakiSelector()
        {
            var assembly = ReplayGuard.FindAssembly("CustomJSONData");
            customNote = assembly?.GetType("CustomJSONData.CustomBeatmap.CustomNoteData", false);
            customBeatmap = assembly?.GetType("CustomJSONData.CustomBeatmap.CustomBeatmapData", false);
            customData = customNote?.GetProperty("customData");
        }
        public bool KnownMap(IReadonlyBeatmapData data) => data != null && data.areValid &&
            (data.GetType() == typeof(BeatmapData) || data.GetType() == customBeatmap);

        public bool Eligible(NoteData note, float start)
        {
            if (note == null || float.IsNaN(note.time) || float.IsInfinity(note.time) || note.time < start ||
                note.gameplayType != NoteData.GameplayType.Normal || note.scoringType != NoteData.ScoringType.Normal ||
                note.isArcHead || note.isArcTail || (note.colorType != ColorType.ColorA && note.colorType != ColorType.ColorB)) return false;
            if (note.GetType() == typeof(NoteData)) return true;
            if (note.GetType() != customNote || customData == null) return false;
            var extra = customData.GetValue(note) as IDictionary<string, object>;
            if (extra == null) return false;
            // Color-only Chroma metadata is safe. Fake/uninteractable/track/animation/
            // unknown metadata is deliberately excluded, regardless of its value.
            foreach (var entry in extra)
                if (entry.Key != "color" && entry.Key != "_color" && entry.Key != "spawnEffect" && entry.Key != "_disableSpawnEffect") return false;
            return true;
        }

        public NoteData Select(IReadonlyBeatmapData map, float start, Random random, bool visualTest = false)
        {
            if (!KnownMap(map) || float.IsNaN(start) || float.IsInfinity(start)) return null;
            var items = map.allBeatmapDataItems;
            if (items == null) return null;
            var candidates = new List<NoteData>();
            // One startup pass, retaining original NoteData references. No copies/mutations.
            for (var node = items.First; node != null; node = node.Next)
            {
                var note = node.Value as NoteData;
                if (Eligible(note, start)) candidates.Add(note);
            }
            // Test mode picks the middle reachable eligible note without consuming RNG.
            return candidates.Count == 0 ? null : candidates[visualTest ? candidates.Count / 2 : random.Next(candidates.Count)];
        }
    }
}
