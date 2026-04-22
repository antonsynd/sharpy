using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents a template string (PEP 750). Template strings look like f-strings
    /// but produce a Template object instead of a formatted string, allowing inspection
    /// and transformation of the interpolation parts.
    /// </summary>
    public class Template : IEnumerable<object>
    {
        /// <summary>The literal string parts (N+1 parts for N interpolations).</summary>
        public string[] Strings { get; }

        /// <summary>The interpolated values.</summary>
        public Interpolation[] Interpolations { get; }

        /// <summary>Create a Template with the given string parts and interpolations.</summary>
        public Template(string[] strings, Interpolation[] interpolations)
        {
            if (strings == null)
                throw new ArgumentNullException(nameof(strings));
            if (interpolations == null)
                throw new ArgumentNullException(nameof(interpolations));
            if (strings.Length != interpolations.Length + 1)
                throw new ArgumentException(
                    $"strings array must have exactly one more element than interpolations (got {strings.Length} strings and {interpolations.Length} interpolations)");

            Strings = strings;
            Interpolations = interpolations;
        }

        /// <summary>Shortcut: returns an array of Interpolation values.</summary>
        public object[] Values
        {
            get
            {
                var values = new object[Interpolations.Length];
                for (int i = 0; i < Interpolations.Length; i++)
                {
                    values[i] = Interpolations[i].Value;
                }
                return values;
            }
        }

        /// <summary>
        /// Formats like an f-string: joins strings with interpolation values.
        /// This makes print(t"Hello {name}") print "Hello world".
        /// </summary>
        public override string ToString()
        {
            if (Interpolations.Length == 0)
                return Strings[0];

            var sb = new StringBuilder();
            for (int i = 0; i < Interpolations.Length; i++)
            {
                sb.Append(Strings[i]);
                sb.Append(Interpolations[i].ToString());
            }
            sb.Append(Strings[Strings.Length - 1]);
            return sb.ToString();
        }

        /// <summary>
        /// Returns a Python-style repr of this Template.
        /// </summary>
        public string Repr()
        {
            var sb = new StringBuilder();
            sb.Append("Template(strings=[");
            for (int i = 0; i < Strings.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append('\'');
                sb.Append(Strings[i]);
                sb.Append('\'');
            }
            sb.Append("], interpolations=[");
            for (int i = 0; i < Interpolations.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(Interpolations[i].Repr());
            }
            sb.Append("])");
            return sb.ToString();
        }

        /// <summary>
        /// Concatenates two templates, combining their strings and interpolations.
        /// The last string of the left template is merged with the first string of the right template.
        /// </summary>
        public static Template operator +(Template left, Template right)
        {
            if (left == null)
                throw new ArgumentNullException(nameof(left));
            if (right == null)
                throw new ArgumentNullException(nameof(right));

            var newStrings = new string[left.Strings.Length + right.Strings.Length - 1];
            var newInterpolations = new Interpolation[left.Interpolations.Length + right.Interpolations.Length];

            // Copy left strings except the last
            for (int i = 0; i < left.Strings.Length - 1; i++)
            {
                newStrings[i] = left.Strings[i];
            }

            // Merge last left string with first right string
            newStrings[left.Strings.Length - 1] = left.Strings[left.Strings.Length - 1] + right.Strings[0];

            // Copy remaining right strings
            for (int i = 1; i < right.Strings.Length; i++)
            {
                newStrings[left.Strings.Length - 1 + i] = right.Strings[i];
            }

            // Copy all interpolations
            Array.Copy(left.Interpolations, 0, newInterpolations, 0, left.Interpolations.Length);
            Array.Copy(right.Interpolations, 0, newInterpolations, left.Interpolations.Length, right.Interpolations.Length);

            return new Template(newStrings, newInterpolations);
        }

        /// <summary>
        /// Iterates over string and interpolation parts interleaved.
        /// Yields strings (as string) and Interpolation objects alternately.
        /// </summary>
        public IEnumerator<object> GetEnumerator()
        {
            for (int i = 0; i < Interpolations.Length; i++)
            {
                if (!string.IsNullOrEmpty(Strings[i]))
                    yield return Strings[i];
                yield return Interpolations[i];
            }
            if (Strings.Length > 0 && !string.IsNullOrEmpty(Strings[Strings.Length - 1]))
                yield return Strings[Strings.Length - 1];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
