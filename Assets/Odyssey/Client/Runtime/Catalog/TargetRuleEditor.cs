using System;
using System.Collections.Generic;
using Odyssey.Domain.Content;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>
    /// ODY-S11-202: the one reusable editor for <see cref="ContentTargetRule"/> (Ability and Effect forms, and the
    /// read-only target hint of the combat panel): TargetSource selector (OdySelect) + minimum/maximum counts + AllowSelf. It edits a
    /// <see cref="TargetRuleModel"/> in place and reports every change through <c>onChanged</c>.
    /// </summary>
    public static class TargetRuleEditor
    {
        public static VisualElement Build(TargetRuleModel model, string namePrefix, bool editable, Action onChanged)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (onChanged == null) throw new ArgumentNullException(nameof(onChanged));

            VisualElement section = OdyUi.Section("Targets", namePrefix + "-target-rule");
            var row = new VisualElement();
            row.AddToClassList(OdyClasses.FormRow);
            section.Add(row);

            List<string> sources = EnumChoices.Names<ContentTargetSource>();
            OdySelect source = OdyUi.Select("Target source", sources, sources.IndexOf(model.Source.ToString()), namePrefix + "-target-source");
            source.ValueChanged += value =>
            {
                if (EnumChoices.TryParse(value, out ContentTargetSource parsed)) model.Source = parsed;
                onChanged();
            };
            row.Add(source);

            IntegerField minimum = OdyUi.IntegerField("Min", EnumChoices.ClampToInt(model.MinimumCount), namePrefix + "-target-min");
            minimum.RegisterValueChangedCallback(evt =>
            {
                model.MinimumCount = evt.newValue;
                onChanged();
            });
            row.Add(minimum);

            IntegerField maximum = OdyUi.IntegerField("Max", EnumChoices.ClampToInt(model.MaximumCount), namePrefix + "-target-max");
            maximum.RegisterValueChangedCallback(evt =>
            {
                model.MaximumCount = evt.newValue;
                onChanged();
            });
            row.Add(maximum);

            Toggle allowSelf = OdyUi.Toggle("Can target self", model.AllowSelf, namePrefix + "-target-allow-self");
            allowSelf.RegisterValueChangedCallback(evt =>
            {
                model.AllowSelf = evt.newValue;
                onChanged();
            });
            section.Add(allowSelf);

            section.SetEnabled(editable);
            return section;
        }

        /// <summary>A one-line human summary, e.g. "Manual selection, 1-2 targets, self allowed".</summary>
        public static string Describe(ContentTargetRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            string count = rule.MinimumCount == rule.MaximumCount
                ? rule.MinimumCount + (rule.MinimumCount == 1 ? " target" : " targets")
                : rule.MinimumCount + "-" + rule.MaximumCount + " targets";
            return EnumChoices.Humanize(rule.TargetSource.ToString()) + ", " + count + (rule.AllowSelf ? ", self allowed" : ", not self");
        }
    }

    /// <summary>Enum &lt;-&gt; dropdown helpers (stable declaration order, invariant names).</summary>
    public static class EnumChoices
    {
        public static List<string> Names<T>() where T : struct, Enum
        {
            var names = new List<string>();
            foreach (T value in (T[])Enum.GetValues(typeof(T))) names.Add(value.ToString());
            return names;
        }

        public static bool TryParse<T>(string? text, out T value) where T : struct, Enum
        {
            value = default;
            if (string.IsNullOrEmpty(text)) return false;
            return Enum.TryParse(text, false, out value) && Enum.IsDefined(typeof(T), value);
        }

        public static int ClampToInt(long value) => value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;

        /// <summary>"ManualSelection" -> "Manual selection".</summary>
        public static string Humanize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var builder = new System.Text.StringBuilder(name.Length + 8);
            for (int index = 0; index < name.Length; index++)
            {
                char c = name[index];
                bool boundary = index > 0 && char.IsUpper(c) && !char.IsUpper(name[index - 1]);
                if (boundary) builder.Append(' ');
                builder.Append(index > 0 && boundary ? char.ToLowerInvariant(c) : c);
            }

            return builder.ToString();
        }
    }
}
