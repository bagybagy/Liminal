using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    [Serializable]
    internal sealed class StaffCreditRoster
    {
        public int schemaVersion;
        public OpeningLine[] opening;
        public string contributorHeading;
        public Contributor[] contributors;
        public string technologyHeading;
        public string[] technologies;
        public string closing;

        [Serializable]
        public sealed class OpeningLine
        {
            public string text;
            public bool heading;
            public bool featured;
        }

        [Serializable]
        public sealed class Contributor
        {
            public string id;
            public string nickname;
            public string role;
            public string model;
            public string effort;
            public Evidence[] evidence;
        }

        [Serializable]
        public sealed class Evidence
        {
            public string file;
            public int line;
            public string sha256;
            public string eventName;
        }

        internal readonly struct DisplayLine
        {
            public readonly string Text;
            public readonly string ParticleText;
            public readonly bool Heading;
            public readonly bool Featured;

            public DisplayLine(string text, bool heading, bool featured = false, string particleText = null)
            {
                Text = text;
                ParticleText = particleText ?? ParticleGlyphText(text);
                Heading = heading;
                Featured = featured;
            }
        }

        public int ContributorCount => contributors.Length;

        public static StaffCreditRoster Load(TextAsset source)
        {
            if (!source) throw new InvalidOperationException("ProductionCredits.json was not found in Resources.");
            var roster = JsonUtility.FromJson<StaffCreditRoster>(source.text);
            if (roster == null || roster.schemaVersion != 1 || roster.opening == null ||
                string.IsNullOrWhiteSpace(roster.contributorHeading) || roster.contributors == null ||
                string.IsNullOrWhiteSpace(roster.technologyHeading) || roster.technologies == null ||
                string.IsNullOrWhiteSpace(roster.closing))
                throw new InvalidOperationException("ProductionCredits.json has an invalid roster structure.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Contributor contributor in roster.contributors)
            {
                if (contributor == null || string.IsNullOrWhiteSpace(contributor.id) ||
                    string.IsNullOrWhiteSpace(contributor.nickname) || string.IsNullOrWhiteSpace(contributor.role))
                    throw new InvalidOperationException("Production credits contain an incomplete contributor.");
                if (!ids.Add(contributor.id))
                    throw new InvalidOperationException("Production credits contain a duplicate contributor UUID.");
                if (string.IsNullOrWhiteSpace(contributor.model)) contributor.model = "Model not recorded";
            }

            return roster;
        }

        public DisplayLine[] BuildDisplayLines()
        {
            var lines = new List<DisplayLine>(opening.Length + contributors.Length * 2 + technologies.Length + 3);
            foreach (OpeningLine line in opening)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.text))
                    throw new InvalidOperationException("Production credits contain an empty opening line.");
                lines.Add(new DisplayLine(line.text, line.heading, line.featured));
            }

            lines.Add(new DisplayLine(contributorHeading, true));
            foreach (Contributor contributor in contributors)
            {
                lines.Add(new DisplayLine(contributor.role, true));
                string identity = contributor.nickname + " / " + contributor.model;
                if (!string.IsNullOrWhiteSpace(contributor.effort))
                    identity += " / " + contributor.effort;
                lines.Add(new DisplayLine(identity, false, false, ParticleGlyphText(identity)));
            }

            lines.Add(new DisplayLine(technologyHeading, true));
            foreach (string technology in technologies)
            {
                if (string.IsNullOrWhiteSpace(technology))
                    throw new InvalidOperationException("Production credits contain an empty technology line.");
                lines.Add(new DisplayLine(technology, false));
            }
            lines.Add(new DisplayLine(closing, true, true));
            return lines.ToArray();
        }

        static string ParticleGlyphText(string text)
        {
            return text.ToUpperInvariant().Replace(".", " POINT ").Replace('-', ' ');
        }
    }
}
