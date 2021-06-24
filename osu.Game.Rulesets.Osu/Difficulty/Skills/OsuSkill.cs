// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public abstract class OsuSkill : Skill
    {
        private double starRatingConstant = .58725; // 7.27* with hr
        // private double starRatingConstant = .6752; // 7.27* without hr

        protected OsuSkill(Mod[] mods) : base(mods)
        {
        }

        /// <summary>
        /// Utility to decay strain over a period of deltaTime.
        /// </summary>
        /// <param name="baseDecay">The rate of decay per object.</param>
        /// <param name="deltaTime">The time between objects.</param>
        /// <param name="beginDecayThreshold">The maximum time between objects before strain will begin decaying geometrically.</param>
        protected double computeDecay(double baseDecay, double deltaTime, double beginDecayThreshold)
        {
            double decay = 0;
            if (deltaTime < beginDecayThreshold)
                decay = baseDecay;
            else // Beyond 500 MS (or whatever beginDecayThreshold is), we decay geometrically to avoid keeping strain going over long breaks.
                decay = Math.Pow(Math.Pow(baseDecay, 1000 / Math.Min(deltaTime, beginDecayThreshold)), deltaTime / 1000);

            return decay;
        }

        /// <summary>
        /// The total derived difficulty from the list of strains based on the starsPerDouble.
        /// </summary>
        protected double calculateDifficultyValue(List<double> strains, double starsPerDouble)
        {
            double difficultyExponent = 1.0 / Math.Log(starsPerDouble, 2);
            double SR = 0;

            // Math here preserves the property that two notes of equal difficulty x, we have their summed difficulty = x*StarsPerDouble
            // This also applies to two sets of notes with equal difficulty.

            for (int i = 0; i < strains.Count; i++)
            {
                SR += Math.Pow(strains[i], difficultyExponent);
            }

            return Math.Pow(SR, 1.0 / difficultyExponent);
        }

        /// <summary>
        /// The total derived difficulty from the list of strains based on the starsPerDouble.
        /// </summary>
        protected double calculateRngStars(List<double> strains, double starsPerDouble, double difficulty)
        {
            double difficultyExponent = 1.0 / Math.Log(starsPerDouble, 2);
            double SR = 0;

            for (int i = 0; i < strains.Count; i++)
            {
                SR += Math.Pow((difficulty - strains[i]) / 2, difficultyExponent);
            }

            return Math.Pow(SR, 1.0 / difficultyExponent);
        }

        /// <summary>
        /// Derives the combined star rating for two seperate star ratings based on the starsPerDouble.
        /// </summary>
        public double combineStarRating(double first, double second, double starsPerDouble)
        {
            double difficultyExponent = 1.0 / Math.Log(starsPerDouble, 2);

            return Math.Pow(Math.Pow(first, difficultyExponent) + Math.Pow(second, difficultyExponent), 1.0 / difficultyExponent);
        }

        /// <summary>
        /// Calculates the display difficulty value for a given list of strains.
        /// </summary>
        public double calculateDisplayDifficultyValue(List<double> strains, double starsPerDouble)
        {
            double difficulty = 0;
            double weight = 1;
            double decayWeight = 0.95;

            // Difficulty is the weighted sum of the highest strains from every section.
            // We're sorting from highest to lowest strain.
            foreach (double strain in strains.OrderByDescending(d => d))
            {
                difficulty += strain * weight;
                weight *= decayWeight;
            }

            return difficulty * starRatingConstant;
        }

        /// <summary>
        /// Returns the calculated display difficulty value representing all <see cref="DifficultyHitObject"/>s that have been processed up to this point.
        /// </summary>
        public abstract double DisplayDifficultyValue();

        protected override abstract void Process(DifficultyHitObject current);

        public override abstract double DifficultyValue();
    }
}
