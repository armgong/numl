﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using numl.Math.LinearAlgebra;
using numl.Math.Functions;
using numl.Math.Probability;
using numl.Utils;

namespace numl.Supervised.NeuralNetwork.Recurrent
{
    /// <summary>
    /// An individual Gated Recurrent Neuron
    /// </summary>
    public class RecurrentNeuron : Neuron
    {
        public const string TimeStepLabel = "TimeStep";

        protected double HtP = 0, DRx = 0, DRh = 0, DZx = 0, DZh = 0, DHh = 0;

        /// <summary>
        /// Gets or sets a map of H state deltas from previous time steps.
        /// </summary>
        protected Dictionary<int, double> DeltaH { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Gets or sets a map of hidden states for the current sequence.
        /// </summary>
        protected Dictionary<int, double> StatesH { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Gets or Sets the hidden (internal) state of the neuron.
        /// </summary>
        public double H { get; set; }

        /// <summary>
        /// Gets or sets the current Reset state value.
        /// </summary>
        public double R { get; set; }

        /// <summary>
        /// Gets or sets the Reset gate bias value.
        /// </summary>
        public double Rb { get; set; }

        /// <summary>
        /// Gets or sets the Reset gate to Hidden state weight value.
        /// <para>This is equivalent to the Ur weight value.</para>
        /// </summary>
        public double Rh { get; set; }

        /// <summary>
        /// Gets or sets the Reset gate to Input weight value.
        /// <para>This is equivalent to the Wr value.</para>
        /// </summary>
        public double Rx { get; set; }

        /// <summary>
        /// Gets or sets the current Update state value.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Gets or sets the Update gate bias value.
        /// </summary>
        public double Zb { get; set; }

        /// <summary>
        /// Gets or sets the Update gate to Hidden weight value.
        /// <para>This is equivalent to the Uz weight value.</para>
        /// </summary>
        public double Zh { get; set; }

        /// <summary>
        /// Gets or sets the Update gate to Input weight value.
        /// <para>This is equivalent to the Wz value.</para>
        /// </summary>
        public double Zx { get; set; }

        /// <summary>
        /// Gets or sets the Recurrent weight value.
        /// </summary>
        public double Hh { get; set; }

        /// <summary>
        /// Gets or sets the reset gate function.
        /// </summary>
        public IFunction ResetGate { get; set; }

        /// <summary>
        /// Gets or sets the update (memory) gate function.
        /// </summary>
        public IFunction MemoryGate { get; set; }

        /// <summary>
        /// Initializes a new Recurrent Neuron.
        /// </summary>
        public RecurrentNeuron() : base()
        {
            this.H = 0d;
            this.Rb = 0d;
            this.Zb = 0d;
            this.Zx = Edge.GetWeight(0.5);
            this.Zh = Edge.GetWeight(0.5);
            this.Rx = Edge.GetWeight(0.5);
            this.Rh = Edge.GetWeight(0.5);
            this.Hh = Edge.GetWeight(1.5);

            if (this.ResetGate == null)
                this.ResetGate = new Math.Functions.SteepLogistic();
            if (this.MemoryGate == null)
                this.MemoryGate = new Math.Functions.SteepLogistic();
            if (this.ActivationFunction == null)
                this.ActivationFunction = new Math.Functions.Tanh();
        }

        /// <summary>
        /// Stores state information prior to computing the error derivatives.
        /// </summary>
        /// <param name="properties">Network training properties object.</param>
        public void State(NetworkTrainingProperties properties)
        {
            this.StatesH[(int) properties[TimeStepLabel]] = this.H;
        }

        /// <summary>
        /// Evaluates the state.
        /// </summary>
        /// <returns></returns>
        public override double Evaluate()
        {
            // guarantee updates to Input
            base.Evaluate();

            if (base.In.Count > 0)
            {
                // is hidden unit - apply memory states
                this.R = this.ResetGate.Compute((this.Rx * this.Input) + (this.Rh * this.H) + this.Rb);
                this.Z = this.MemoryGate.Compute((this.Zx * this.Input) + (this.Zh * this.H) + this.Zb);

                this.HtP = this.ActivationFunction.Compute(this.Input + (this.R * this.H) * this.Hh);

                this.H = (1.0 - this.Z) * this.H + this.Z * this.HtP;

                this.Output = H;
            }

            return this.Output;
        }

        /// <summary>
        /// Returns the error given the supplied error derivative.
        /// </summary>
        /// <param name="t">The error from the next layer.</param>
        /// <param name="properties">Network training properties object.</param>
        /// <returns></returns>
        public override double Error(double t, NetworkTrainingProperties properties)
        {
            //TODO: Return the correct error.
            _DeltaL = Delta;

            if (Out.Count == 0)
                Delta = delta = -(t - Output);

            else
            {
                int timestep = (int) properties[TimeStepLabel];
                int seqlength = (int) properties[nameof(GatedRecurrentGenerator.SequenceLength)];

                double htm1 = this.StatesH.ContainsKey(timestep - 1) ? this.StatesH[timestep - 1] : 0;
                double h = (this.StatesH.ContainsKey(timestep) ? this.StatesH[timestep] : 0);

                double seqmod = (1.0 / seqlength);

                if (In.Count > 0 && Out.Count > 0)
                {
                    double dyhh = (1.0 - this.Z), dyhz = this.HtP - h;
                    double dhtP = this.ActivationFunction.Derivative(this.Input + (this.R * htm1) * this.Hh);
                    dhtP = dhtP * dyhh;

                    double dr = this.ResetGate.Derivative((this.Rx * this.Input) + (this.Rh * h) + this.Rb);
                    double dz = this.MemoryGate.Derivative((this.Zx * this.Input) + (this.Zh * h) + this.Zb);

                    this.DRx = (seqmod * (dr * this.Input)); this.DRh = (seqmod * (dr * h));
                    this.DZx = (seqmod * (dz * this.Input)); this.DZh = (seqmod * (dz * h));

                    delta = Out.Sum(e => e.Weight * t) + this.Hh * dhtP;

                    this.DeltaH[timestep] = this.DeltaH.GetValueOrDefault(timestep, 0) + this.DeltaH.GetValueOrDefault(timestep + 1, 0);
                }

                this.Delta = Out.Sum(s => s.Target.delta * this.Output) + this.DeltaH[timestep];
            }

            if (this.In.Count > 0)
            {
                for (int edge = 0; edge < this.In.Count; edge++)
                {
                    this.In[edge].Source.Error(this.Delta, properties);
                }
            }

            return Delta;
        }

        /// <summary>
        /// Updates the weights using the supplied (<see cref="NetworkTrainingProperties"/>)
        /// </summary>
        /// <param name="properties">Network training properties.</param>
        public override void Update(NetworkTrainingProperties properties)
        {
            if (!this.Constrained)
            {
                double lm = (properties.Lambda / (int) properties[nameof(GatedRecurrentGenerator.SequenceLength)]);

                this.Rx = this.Rx - properties.LearningRate * (this.DRx + (lm * this.Rx));
                this.Rh = this.Rh - properties.LearningRate * (this.DRh + (lm * this.Rh));

                this.Zx = this.Zx - properties.LearningRate * (this.DZx + (lm * this.Zx));
                this.Zh = this.Zh - properties.LearningRate * (this.DZh + (lm * this.Zh));

                this.Hh = this.Hh - properties.LearningRate * (this.DHh + (lm * this.Hh));
            }

            for (int edge = 0; edge < this.In.Count; edge++)
            {
                Delta = (1.0 / properties.Examples) * Delta;

                if (!this.In[edge].Source.IsBias)
                    Delta = Delta + ((properties.Lambda / properties.Examples) * this.In[edge].Weight);

                if (!this.Constrained)
                {
                    this.In[edge].Weight = this.In[edge].Weight - properties.LearningRate * Delta;
                }

                this.In[edge].Source.Update(properties);
            }
        }

        /// <summary>
        /// Resets the state of the current neuron.
        /// </summary>
        /// <param name="properties">Network training properties.</param>
        public override void Reset(NetworkTrainingProperties properties)
        {
            this.H = 0; this.HtP = 0; this.DRx = 0; this.DRh = 0; this.DZx = 0; this.DZh = 0; this.DHh = 0;

            this.DeltaH = new Dictionary<int, double>();
            this.StatesH = new Dictionary<int, double>();

            base.Reset(properties);
        }
    }
}
