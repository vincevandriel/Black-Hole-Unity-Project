using BlackHole.Staging.Simulation;
using UnityEngine;

namespace DTET.VisualPrototype
{
    internal static class ReducedBinaryFlowBinding
    {
        public static void Bind(Material material, ReducedBinaryFlowState state)
        {
            material.SetVector("_FlowA",V(state.BodyA.PositionRg,state.BodyA.MassFraction));
            material.SetVector("_FlowB",V(state.BodyB.PositionRg,state.BodyB.MassFraction));
            material.SetVector("_FlowRemnant",V(state.Remnant.PositionRg,state.Remnant.MassFraction));
            material.SetVector("_FlowARadii",new Vector4((float)state.BodyA.InnerRadiusRg,(float)state.BodyA.OuterRadiusRg,(float)state.BodyA.HorizonRadiusRg,0));
            material.SetVector("_FlowBRadii",new Vector4((float)state.BodyB.InnerRadiusRg,(float)state.BodyB.OuterRadiusRg,(float)state.BodyB.HorizonRadiusRg,0));
            material.SetVector("_FlowRemnantRadii",new Vector4((float)state.Remnant.InnerRadiusRg,(float)state.Remnant.OuterRadiusRg,(float)state.Remnant.HorizonRadiusRg,0));
            material.SetVector("_FlowReservoir",new Vector4((float)state.ReservoirInnerRadiusRg,(float)state.ReservoirOuterRadiusRg,(float)state.PatternCos2,(float)state.PatternSin2));
            material.SetVector("_FlowWeights",new Vector4((float)state.BinaryWeight,(float)state.RemnantWeight,state.IsCommonHorizon?1:0,0));
            material.SetVector("_FlowStreamAStart",V(state.StreamA.StartRg));material.SetVector("_FlowStreamAEnd",V(state.StreamA.EndRg));
            material.SetVector("_FlowStreamABow",V(state.StreamA.BowNormal,state.StreamA.ChordLengthSquaredRg2));
            material.SetVector("_FlowStreamBStart",V(state.StreamB.StartRg));material.SetVector("_FlowStreamBEnd",V(state.StreamB.EndRg));
            material.SetVector("_FlowStreamBBow",V(state.StreamB.BowNormal,state.StreamB.ChordLengthSquaredRg2));
        }
        private static Vector4 V(NumericalVector3 v,double w=0)=>new Vector4((float)v.X,(float)v.Y,(float)v.Z,(float)w);
    }
}
