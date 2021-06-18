// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuSkill
    {
        protected override int HistoryLength => 2;

        // Global Constants for interpolating difficulty between objects SRs
        private double snapStarsPerDouble = 1.125;
        private double flowStarsPerDouble = 1.1;
        private double combinedStarsPerDouble = 1.15;

        private double currOtherStrain = 1;
        private double currSnapStrain = 1;
        private double currFlowStrain = 1;

        private List<double> snapStrains = new List<double>();
        private List<double> flowStrains = new List<double>();

        private double baseDecay => 0.75;
        private int beginDecayThreshold => 500;
        private double distanceConstant = 3.5;

        // Global Constants for the different types of aim.
        private double snapStrainMultiplier = 6.727;
        private double flowStrainMultiplier = 16.272;
        private double hybridStrainMultiplier = 0;//15;//30.727;
        private double sliderStrainMultiplier = 65;
        private double totalStrainMultiplier = .1025;

        public Aim(Mod[] mods)
            : base(mods)
        {
        }

        /// <summary>
        /// Calculates the difficulty to flow from the previous <see cref="OsuDifficultyHitObject"/> the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        private double flowStrainAt(OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj,
                                    Vector2 prevVector, Vector2 currVector, Vector2 nextVector)
        {
            // double strain = 0;

            // var observedCurrDistance = currVector;//Vector2.Subtract(currVector, Vector2.Multiply(prevVector, (float)0.1));
            // var observedPrevDistance = prevVector;//Vector2.Subtract(prevVector, Vector2.Multiply(currVector, (float)0.1));
            //
            //
            // strain = osuCurrObj.FlowProbability * ((0.5 * observedCurrDistance.Length + 0.5 * osuPrevObj.FlowProbability * observedPrevDistance.Length)
            //          + momentumChange * osuPrevObj.FlowProbability);
                                // angularMomentumChange * osuPrevObj.FlowProbability));

            prevVector = Vector2.Multiply(prevVector, (float)(osuPrevObj.StrainTime / (osuPrevObj.StrainTime - 10)));
            currVector = Vector2.Multiply(currVector, (float)(osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 10)));
            nextVector = Vector2.Multiply(nextVector, (float)(osuNextObj.StrainTime / (osuNextObj.StrainTime - 10)));

            double strain = 0;

            double averageVel = (prevVector.Length * osuPrevObj.FlowProbability
                                + currVector.Length * osuCurrObj.FlowProbability
                                + nextVector.Length * osuNextObj.FlowProbability)
                                  / Math.Max(1, osuPrevObj.FlowProbability + osuCurrObj.FlowProbability + osuNextObj.FlowProbability);

            double averageAngle = (osuPrevObj.Angle + osuCurrObj.Angle + osuNextObj.Angle) / 3;

            double velVariance = Math.Min(averageVel, (0.5 + 0.5 * osuPrevObj.FlowProbability) * Math.Abs(averageVel - prevVector.Length )
                                                    + (0.5 + 0.5 * osuCurrObj.FlowProbability) * Math.Abs(averageVel - currVector.Length)
                                                    + (0.5 + 0.5 * osuNextObj.FlowProbability) * Math.Abs(averageVel - nextVector.Length)) / 3;

            double angularVariance = (//osuPrevObj.FlowProbability * Math.Abs(averageAngle - osuPrevObj.Angle)
                                      osuCurrObj.FlowProbability * Math.Abs(averageAngle - osuCurrObj.Angle)
                                      + osuNextObj.FlowProbability * Math.Abs(averageAngle - osuNextObj.Angle)) / (4 * Math.PI);

            strain = (osuPrevObj.FlowProbability + osuCurrObj.FlowProbability + osuNextObj.FlowProbability)
                    * (averageVel + Math.Max(angularVariance * averageVel,
                       velVariance));

            return  strain;
        }

        /// <summary>            currVector = Vector2.Multiply(currVector, (float)snapScaling(osuCurrObj.JumpDistance / 100));
        /// Alters the distance traveled for snapping to match the results from Fitt's law.
        /// </summary>
        private double snapScaling(double distance)
        {
            if (distance == 0)
                return 0;
            else
                return (distanceConstant * Math.Log(1 + distance / distanceConstant) / Math.Log(2)) / distance;

            // if (distance <= distanceConstant)
            //     return 1;
            // else
            //     return (distanceConstant + (distance - distanceConstant) * (Math.Log(1 + (distance - distanceConstant) / Math.Sqrt(2)) / Math.Log(2)) / (distance - distanceConstant)) / distance;
        }

        /// <summary>
        /// Calculates the difficulty to snap from the previous <see cref="OsuDifficultyHitObject"/> the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        private double snapStrainAt(OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj,
                                    Vector2 prevVector, Vector2 currVector, Vector2 nextVector)
        {
            double strain = 0;

            currVector = Vector2.Multiply(currVector, (float)(snapScaling(osuCurrObj.JumpDistance / 100) * (osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 20))));
            prevVector = Vector2.Multiply(prevVector, (float)(snapScaling(osuPrevObj.JumpDistance / 100) * (osuPrevObj.StrainTime / (osuPrevObj.StrainTime - 20))));
            nextVector = Vector2.Multiply(nextVector, (float)(snapScaling(osuNextObj.JumpDistance / 100) * (osuNextObj.StrainTime / (osuNextObj.StrainTime - 20))));

            double prevCurrBonusDistance = Math.Max(0, Vector2.Add(currVector, prevVector).Length - Math.Max(currVector.Length, prevVector.Length));
            double currNextBonusDistance = Math.Max(0, Vector2.Add(currVector, nextVector).Length - Math.Max(currVector.Length, nextVector.Length));

            double averageVel = (prevVector.Length * osuPrevObj.SnapProbability
                                + currVector.Length * osuCurrObj.SnapProbability
                                + nextVector.Length * osuNextObj.SnapProbability)
                                  / Math.Max(1, osuPrevObj.SnapProbability + osuCurrObj.SnapProbability + osuNextObj.SnapProbability);

            strain = (osuPrevObj.SnapProbability + osuCurrObj.SnapProbability + osuNextObj.SnapProbability)
                    * (averageVel + Math.Min(0.25 * averageVel, Math.Max((osuPrevObj.SnapProbability * osuCurrObj.SnapProbability) * prevCurrBonusDistance,
                            (osuPrevObj.SnapProbability * osuCurrObj.SnapProbability) * currNextBonusDistance) / 2));

            return strain;// * Math.Min(osuNextObj.StrainTime / (osuNextObj.StrainTime - 20), Math.Min(osuCurrObj.StrainTime / (osuCurrObj.StrainTime - 10), osuPrevObj.StrainTime / (osuPrevObj.StrainTime - 10)));
        }

        /// <summary>
        /// Calculates the difficulty to flow from the previous <see cref="OsuDifficultyHitObject"/> the current <see cref="OsuDifficultyHitObject"/> with context to odd patterns.
        /// </summary>
        private double hybridStrainAt(OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj,
                                      Vector2 prevVector, Vector2 currVector, Vector2 nextVector)
        {
            double averageVel = (prevVector.Length + currVector.Length + nextVector.Length) / 3;

            double geoAverageVel = Math.Pow(prevVector.Length * currVector.Length * nextVector.Length, 1 / 3);

            double velVariance = Math.Abs(averageVel - prevVector.Length)
                                + Math.Abs(averageVel - currVector.Length)
                                + Math.Abs(averageVel - nextVector.Length);

            return osuCurrObj.FlowProbability * Math.Pow(geoAverageVel * velVariance, 0.5);
        }

        /// <summary>
        /// Calculates the estimated difficulty associated with the slider movement from the previous <see cref="OsuDifficultyHitObject"/> to the current <see cref="OsuDifficultyHitObject"/>.
        /// </summary>
        private double sliderStrainAt(OsuDifficultyHitObject osuPrevObj, OsuDifficultyHitObject osuCurrObj, OsuDifficultyHitObject osuNextObj)
        {
            double strain = Math.Max(osuPrevObj.TravelDistance / osuPrevObj.StrainTime,
                                     osuCurrObj.TravelDistance / osuCurrObj.StrainTime);

            return strain;
        }

        protected override void Process(DifficultyHitObject current)
        {
            if (Previous.Count > 1)
            {
                var osuNextObj = (OsuDifficultyHitObject)current;
                var osuCurrObj = (OsuDifficultyHitObject)Previous[0];
                var osuPrevObj = (OsuDifficultyHitObject)Previous[1];
                // Since it is easier to get history, we take the previous[0] as our current, so we can see our "next"

                Vector2 nextVector = Vector2.Divide(osuNextObj.DistanceVector, (float)osuNextObj.StrainTime);
                Vector2 currVector = Vector2.Divide(osuCurrObj.DistanceVector, (float)osuCurrObj.StrainTime);
                Vector2 prevVector = Vector2.Divide(osuPrevObj.DistanceVector, (float)osuPrevObj.StrainTime);

                double snapStrain = snapStrainAt(osuPrevObj,
                                                 osuCurrObj,
                                                 osuNextObj,
                                                 prevVector,
                                                 currVector,
                                                 nextVector);

                double flowStrain = flowStrainAt(osuPrevObj,
                                                 osuCurrObj,
                                                 osuNextObj,
                                                 prevVector,
                                                 currVector,
                                                 nextVector);

                double hybridStrain = hybridStrainAt(osuPrevObj,
                                                     osuCurrObj,
                                                     osuNextObj,
                                                     prevVector,
                                                     currVector,
                                                     nextVector);

                double sliderStrain = sliderStrainAt(osuPrevObj,
                                                     osuCurrObj,
                                                     osuNextObj);
                // Currently passing all available data, just incase it is useful for calculation.

                currSnapStrain *= computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold);
                currSnapStrain += snapStrain * snapStrainMultiplier;

                currFlowStrain *= computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold);
                currFlowStrain += flowStrain * flowStrainMultiplier;

                currOtherStrain *= computeDecay(baseDecay, osuCurrObj.StrainTime, beginDecayThreshold);
                currOtherStrain += hybridStrain * hybridStrainMultiplier + sliderStrain * sliderStrainMultiplier;

                double totalStrain = totalStrainMultiplier * (currSnapStrain + currFlowStrain + currOtherStrain);

                if (currSnapStrain > currFlowStrain)
                    snapStrains.Add(totalStrain);
                else
                    flowStrains.Add(totalStrain);
            }
        }

        public override double DisplayDifficultyValue()
        {
            double flowStarRating = calculateDisplayDifficultyValue(flowStrains, flowStarsPerDouble);
            double snapStarRating = calculateDisplayDifficultyValue(snapStrains, snapStarsPerDouble);

            return combineStarRating(flowStarRating, snapStarRating, combinedStarsPerDouble);
        }

        public override double DifficultyValue()
        {
            double flowStarRating = calculateDifficultyValue(flowStrains, flowStarsPerDouble);
            double snapStarRating = calculateDifficultyValue(snapStrains, snapStarsPerDouble);

            return combineStarRating(flowStarRating, snapStarRating, combinedStarsPerDouble);
        }
    }
}
