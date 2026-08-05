using System;
using AccardND.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace AccardND.GameCore.Tests
{
    public sealed class LocalizationCatalogTests
    {
        [Test]
        public void ItalianCatalog_ResolvesStaticAndFormattedText()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            Assert.That(LocalizationSettings.ProjectLocale, Is.Not.Null);
            Assert.That(LocalizationSettings.ProjectLocale.Identifier.Code, Is.EqualTo("it"));

            LocalizationSettings.SelectedLocale = LocalizationSettings.ProjectLocale;
            Assert.That(GameText.Get(GameTextKeys.Login.Title), Is.EqualTo("ACCEDI"));
            Assert.That(
                GameText.Format(GameTextKeys.Login.Welcome, "Ada"),
                Is.EqualTo("Benvenuto, Ada."));
            Assert.That(
                GameText.Format(GameTextKeys.Consumables.SecondChanceUsed, 3),
                Is.EqualTo("Seconda Chance: 3 carte tornano dal cimitero al mazzo."));
            Assert.That(
                GameText.Format(
                    GameTextKeys.Combat.ImpossibleAttackDetailed,
                    "A",
                    10,
                    2,
                    string.Empty,
                    "D6",
                    "B",
                    5,
                    3,
                    string.Empty,
                    4),
                Is.EqualTo("0%: A arriva al massimo a 10 (2 + D6), mentre B parte da 5 (3 + D4:1)."));
        }

        [Test]
        public void SkipText_ResolvesItalianCatalogAndEnglishFallbacks()
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            Locale original = LocalizationSettings.SelectedLocale;
            Locale english = Locale.CreateLocale(new LocaleIdentifier("en"));

            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.ProjectLocale;
                Assert.That(GameText.Get(GameTextKeys.Combat.ActionSkip), Is.EqualTo("Salta"));
                Assert.That(
                    GameText.Format(GameTextKeys.Combat.SkipTurnMessage, "Arkon"),
                    Is.EqualTo("Arkon salta il turno."));

                LocalizationSettings.SelectedLocale = english;
                Assert.That(
                    GameText.GetLocalizedFallback(
                        GameTextKeys.Combat.ActionSkip,
                        "Salta",
                        "Skip"),
                    Is.EqualTo("Skip"));
                Assert.That(
                    GameText.GetLocalizedFallback(
                        GameTextKeys.Combat.SkipTurnLog,
                        "SALTA - {0} passa senza agire.",
                        "SKIP - {0} passes without acting.",
                        "Arkon"),
                    Is.EqualTo("SKIP - Arkon passes without acting."));
                Assert.That(
                    GameText.GetLocalizedFallback(
                        GameTextKeys.PvpError.AbilityRequiresAction,
                        "Dopo aver usato un'abilitÃ  devi attaccare o equipaggiarti.",
                        "After using an ability, you must attack or equip."),
                    Is.EqualTo("After using an ability, you must attack or equip."));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = original;
                UnityEngine.Object.DestroyImmediate(english);
            }
        }
    }
}
