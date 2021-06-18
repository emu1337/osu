// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Tap : OsuSkill
    {
        protected override int HistoryLength => 32;

        private double starsPerDouble => 1.075;

        private List<double> strains = new List<double>();

        private double currentStrain = 1;
        private double singleStrain = 1;

        private double baseDecay => 0.9;
        private int beginDecayThreshold => 500;
        private int strainTimeBuffRange = 75;
        private double avgStrainTime = 50;

        // Global Tap Strain Multiplier.
        private double singleMultiplier = 1.575;
        private double strainMultiplier = 1.75;
        private double rhythmMultiplier = 1.0;

        public Tap(Mod[] mods)
            : base(mods)
        {
        }

        private bool isRatioEqual(double ratio, double a, double b)
        {
            return a + 15 > ratio * b && a - 15 < ratio * b;
        }

        /// <summary>
        /// Calculates a rhythm multiplier for the difficulty of the tap associated with historic data of the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        private double calculateRhythmDifficulty()
        {
            // {doubles, triplets, quads, quints, 6-tuplets, 7 Tuplets, greater}
            double[] islandSizes = {0, 0, 0, 0, 0, 0, 0};
            double[] islandTimes = {0, 0, 0, 0, 0, 0, 0};
            int islandSize = 0;
            double specialTransitionCount = 0;

            bool firstDeltaSwitch = false;

            for (int i = 1; i < Previous.Count; i++)
            {
                double prevDelta = ((OsuDifficultyHitObject)Previous[i - 1]).StrainTime;
                double currDelta = ((OsuDifficultyHitObject)Previous[i]).StrainTime;

                if (isRatioEqual(1.5, prevDelta, currDelta) || isRatioEqual(1.5, currDelta, prevDelta))
                {
                    if (Previous[i - 1].BaseObject is Slider || Previous[i].BaseObject is Slider)
                        specialTransitionCount += 250.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength);
                    else
                        specialTransitionCount += 500.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength);
                }

                if (i < Previous.Count)
                    avgStrainTime += currDelta;

                if (firstDeltaSwitch)
                {
                    if (isRatioEqual(1.0, prevDelta, currDelta))
                    {
                        islandSize++; // island is still progressing, count size.
                    }
                    else if (prevDelta > currDelta * 1.25) // we're speeding up
                    {
                        if (islandSize > 6)
                        {
                            islandTimes[6] = islandTimes[6] + 100.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength);
                            islandSizes[6] = islandSizes[6] + 1;
                        }
                        else
                        {
                            islandTimes[islandSize] = islandTimes[islandSize] + 100.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength);
                            islandSizes[islandSize] = islandSizes[islandSize] + 1;
                        }

                        islandSize = 0; // reset and count again, we sped up (usually this could only be if we did a 1/2 -> 1/3 -> 1/4) (or 1/1 -> 1/2 -> 1/4)
                    }
                    else // we're not the same or speeding up, must be slowing down.
                    {
                        if (islandSize > 6)
                        {
                            islandTimes[6] = islandTimes[6] + 100.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength);
                            islandSizes[6] = islandSizes[6] + 1;
                        }
                        else
                        {
                            islandTimes[islandSize] = islandTimes[islandSize] + 100.0 / Math.Sqrt(prevDelta * currDelta) * ((double)i / HistoryLength) ;
                            islandSizes[islandSize] = islandSizes[islandSize] + 1;
                        }

                        firstDeltaSwitch = false; // stop counting island until next speed up.
                    }
                }
                else if (prevDelta >  1.25 * currDelta) // we want to be speeding up.
                {
                    // Begin counting island until we slow again.
                    firstDeltaSwitch = true;
                    islandSize = 0;
                }
            }

            double rhythmComplexitySum = 0.0;

            for (int i = 0; i < islandSizes.Length; i++)
            {
                if (islandSizes[i] != 0)
                    rhythmComplexitySum += islandTimes[i] / Math.Pow(islandSizes[i], .5); // sum the total amount of rhythm variance, penalizing for repeated island sizes.
            }

            rhythmComplexitySum += specialTransitionCount; // add in our 1.5 * transitions

            avgStrainTime /= (Previous.Count);

            return Math.Sqrt(4 + rhythmComplexitySum * rhythmMultiplier) / 2;
        }

        protected override void Process(DifficultyHitObject current)
        {
            if (Previous.Count > 0)
            {
                var osuCurrObj = (OsuDifficultyHitObject)current;

                double strainValue = .25;

                double strainTime = (Math.Max(50, current.DeltaTime) + Math.Max(50, Previous[0].DeltaTime)) / 2;

                double rhythmComplexity = calculateRhythmDifficulty(); // equals 1 with no rhythm difficulty, otherwise scales with a sqrt

                strainTime = (strainTime - 25);

                strainValue += strainTimeBuffRange / strainTime;

            // if (strainTimeBuffRange / strainTime > 1) // scale tap value for high BPM (above 200).
            //     strainValue += Math.Pow(strainTimeBuffRange / strainTime, 2);
            // else
            //     strainValue += Math.Pow(strainTimeBuffRange / strainTime, 1);

                singleStrain *= computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold);
                singleStrain += (1.0 + osuCurrObj.SnapProbability) * strainValue * singleMultiplier;

                currentStrain *= Math.Pow(computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold), Math.Max(1, osuCurrObj.StrainTime / avgStrainTime));
                currentStrain += strainValue * strainMultiplier;

                strains.Add(Math.Max(Math.Min((1 / (1 - baseDecay)) * strainValue * strainMultiplier, // prevent over buffing strain past death stream level
                                          currentStrain * rhythmComplexity),
                                singleStrain)); // we use a seperate strain for singles to not complete nuke boring 1-2 maps
            }
        }


        public override double DisplayDifficultyValue()
        {
            return calculateDisplayDifficultyValue(strains, starsPerDouble);
        }

        public override double DifficultyValue()
        {
            return calculateDifficultyValue(strains, starsPerDouble);
        }
    }
}
