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

        private double baseDecay => 0.9;
        private int beginDecayThreshold => 500;
        private int strainTimeBuffRange = 75;
        private double avgStrainTime = 50;

        // Global Tap Strain Multiplier.
        private double strainMultiplier = 1.725;
        private double rhythmMultiplier = 0.625;

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
            int previousIslandSize = -1;
            double[] islandTimes = {0, 0, 0, 0, 0, 0, 0};
            int islandSize = 0;
            double specialTransitionCount = 0;

            bool firstDeltaSwitch = false;

            for (int i = 1; i < Previous.Count; i++)
            {
                double prevDelta = ((OsuDifficultyHitObject)Previous[i - 1]).StrainTime;
                double currDelta = ((OsuDifficultyHitObject)Previous[i]).StrainTime;
                double geoAvgDelta = Math.Sqrt(prevDelta * currDelta);

                if (isRatioEqual(1.5, prevDelta, currDelta) || isRatioEqual(1.5, currDelta, prevDelta))
                {
                    if (Previous[i - 1].BaseObject is Slider || Previous[i].BaseObject is Slider)
                        specialTransitionCount += 100.0 / geoAvgDelta * ((double)i / HistoryLength);
                    else
                        specialTransitionCount += 200.0 / geoAvgDelta * ((double)i / HistoryLength);
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
                            if (previousIslandSize == 6)
                                islandTimes[6] = islandTimes[6] + 100.0 / geoAvgDelta * ((double)i / HistoryLength);
                            else
                                islandTimes[6] = islandTimes[6] + 200.0 / geoAvgDelta * ((double)i / HistoryLength);

                            previousIslandSize = 6;
                        }
                        else
                        {
                            if (previousIslandSize == islandSize)
                                islandTimes[islandSize] = islandTimes[islandSize] + 100.0 / geoAvgDelta * ((double)i / HistoryLength);
                            else
                                islandTimes[islandSize] = islandTimes[islandSize] + 200.0 / geoAvgDelta * ((double)i / HistoryLength);

                            previousIslandSize = islandSize;
                        }

                        if (prevDelta > currDelta * 1.25) // we're not the same or speeding up, must be slowing down
                            firstDeltaSwitch = false;

                        islandSize = 0; // reset and count again, we sped up (usually this could only be if we did a 1/2 -> 1/3 -> 1/4) (or 1/1 -> 1/2 -> 1/4)
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

            for (int i = 0; i < islandTimes.Length; i++)
            {
                rhythmComplexitySum += islandTimes[i]; // sum the total amount of rhythm variance
            }

            rhythmComplexitySum += specialTransitionCount; // add in our 1.5 * transitions

            avgStrainTime /= (Previous.Count);

            return Math.Min(1.5, Math.Sqrt(4 + rhythmComplexitySum * rhythmMultiplier) / 2);
        }

        protected override void Process(DifficultyHitObject current)
        {
            if (Previous.Count > 0)
            {
                var osuCurrObj = (OsuDifficultyHitObject)current;

                double strainValue = .25;

                double strainTime = (current.DeltaTime + Previous[0].DeltaTime) / 2;

                if (strainTime < 50)
                    strainTime = (strainTime + 150) / 4; //don't want to cap BPM at 300, but also don't want to astro overweight high bpm or div by 0.

                double rhythmComplexity = calculateRhythmDifficulty(); // equals 1 with no rhythm difficulty, otherwise scales with a sqrt

                strainTime = (strainTime - 25);

                strainValue += strainTimeBuffRange / strainTime;


                currentStrain *= Math.Pow(computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold), Math.Max(1, osuCurrObj.StrainTime / avgStrainTime));
                currentStrain += (1.0 + 0.5 * osuCurrObj.SnapProbability) * strainValue * strainMultiplier;

                strains.Add(currentStrain * Math.Min(1.5, rhythmComplexity));
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
