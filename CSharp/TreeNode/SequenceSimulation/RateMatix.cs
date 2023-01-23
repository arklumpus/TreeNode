using MathNet.Numerics.LinearAlgebra;
using PhyloTree.TreeBuilding;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace PhyloTree.SequenceSimulation
{
    /// <summary>
    /// Represents a rate matrix for a continuous-type Markov chain model. This type cannot be instantiated directly,
    /// please use <see cref="MutableRateMatrix"/> or <see cref="ImmutableRateMatrix"/> instead, or access the
    /// static members for some pre-baked common rate matrices for DNA and protein evolution.
    /// </summary>
    public abstract partial class RateMatrix
    {
        internal abstract Matrix<double> GetMatrix();
        internal abstract double[] GetEquilibriumFrequencies();
        internal abstract char[] GetStates();
        internal abstract MatrixExponential GetExponential();
        internal RateMatrix() { }

        /// <summary>
        /// Gets the states for the character to which the rate matrix applies.
        /// </summary>
        public abstract ImmutableArray<char> States { get; }

        /// <summary>
        /// Gets the equilibrium frequences of the rate matrix.
        /// </summary>
        public abstract ImmutableArray<double> EquilibriumFrequencies { get; }

        /// <summary>
        /// Gets the rate of going from state number <paramref name="from"/> to state number
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row number.</param>
        /// <param name="to">The column number.</param>
        /// <returns>The rate of going from state number <paramref name="from"/> to state number <paramref name="to"/>.
        /// </returns>
        public abstract double this[int from, int to] { get; }

        /// <summary>
        /// Gets the rate of going from state <paramref name="from"/> to state
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row state.</param>
        /// <param name="to">The column state.</param>
        /// <returns>The rate of going from state <paramref name="from"/> to state <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state is not part of the rate matrix.</exception>
        public abstract double this[char from, char to] { get; }
    }

    /// <summary>
    /// Represents a rate matrix whose values can be changed after initialisation.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Please do not use this class. Use PhyloTree.SequenceSimulation.RateMatrix or PhyloTree.SequenceSimulation.MutableRateMatrix instead.")]
    public abstract class IMutableRateMatrix : RateMatrix
    {
        /// <summary>
        /// Gets the rate of going from state number <paramref name="from"/> to state number
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row number.</param>
        /// <param name="to">The column number.</param>
        /// <returns>The rate of going from state number <paramref name="from"/> to state number <paramref name="to"/>.
        /// </returns>
        public sealed override double this[int from, int to] => GetThis(from, to);

        /// <summary>
        /// Gets the rate of going from state number <paramref name="from"/> to state number
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row number.</param>
        /// <param name="to">The column number.</param>
        /// <returns>The rate of going from state number <paramref name="from"/> to state number <paramref name="to"/>.
        /// </returns>
        protected internal abstract double GetThis(int from, int to);

        /// <summary>
        /// Gets the rate of going from state <paramref name="from"/> to state
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row state.</param>
        /// <param name="to">The column state.</param>
        /// <returns>The rate of going from state <paramref name="from"/> to state <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state is not part of the rate matrix.</exception>
        public sealed override double this[char from, char to] => GetThis(from, to);

        /// <summary>
        /// Gets the rate of going from state <paramref name="from"/> to state
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row state.</param>
        /// <param name="to">The column state.</param>
        /// <returns>The rate of going from state <paramref name="from"/> to state <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state is not part of the rate matrix.</exception>
        protected internal abstract double GetThis(char from, char to);
    }

    /// <summary>
    /// Represents a rate matrix whose values can be changed after initialisation.
    /// </summary>
#pragma warning disable 0618
    public class MutableRateMatrix : IMutableRateMatrix
    {
#pragma warning restore 0618
        /// <summary>
        /// Gets the states for the character to which the rate matrix applies.
        /// </summary>
        public override ImmutableArray<char> States { get; }

        private readonly double[,] rates;
        private double[] equilibriumFrequencies;
        private MatrixExponential exponential;

        /// <summary>
        /// Gets or sets the rate of going from state number <paramref name="from"/> to state number
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned, but this value cannot be set.
        /// </summary>
        /// <param name="from">The row number.</param>
        /// <param name="to">The column number.</param>
        /// <returns>The rate of going from state number <paramref name="from"/> to state number <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state index is &lt; 0 or greater than the
        /// number of states in the rate matrix.</exception>
        /// <exception cref="ArgumentException">Thrown when attempting to set the value of a diagonal entry.</exception>
        public new double this[int from, int to]
        {
            get
            {
                if (from >= 0 && from < States.Length)
                {
                    if (to >= 0 && to < States.Length)
                    {
                        if (from != to)
                        {
                            return rates[from, to];
                        }
                        else
                        {
                            double tbr = 0;

                            for (int i = 0; i < States.Length; i++)
                            {
                                if (i != from)
                                {
                                    tbr += rates[from, i];
                                }
                            }

                            return -tbr;
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(to));
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(from));
                }
            }

            set
            {
                if (from >= 0 && from < States.Length)
                {
                    if (to >= 0 && to < States.Length)
                    {
                        if (from != to)
                        {
                            rates[from, to] = value;
                            this.equilibriumFrequencies = null;
                            this.exponential = null;
                        }
                        else
                        {
                            throw new ArgumentException("The value of the diagonal entries of a rate matrix cannot be set!");
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(to));
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(from));
                }
            }
        }

        /// <summary>
        /// Gets or sets the rate of going from state <paramref name="from"/> to state
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned, but this value cannot be set.
        /// </summary>
        /// <param name="from">The row state.</param>
        /// <param name="to">The column state.</param>
        /// <returns>The rate of going from state <paramref name="from"/> to state <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state is not part of the rate matrix.</exception>
        /// <exception cref="ArgumentException">Thrown when attempting to set the value of a diagonal entry.</exception>
        public new double this[char from, char to]
        {
            get
            {
                int fromInt = States.IndexOf(from);
                int toInt = States.IndexOf(to);

                if (fromInt < 0)
                {
                    throw new ArgumentException("Character " + from + " is not found in the rate matrix!", nameof(from));
                }

                if (toInt < 0)
                {
                    throw new ArgumentException("Character " + to + " is not found in the rate matrix!", nameof(to));
                }

                return this[fromInt, toInt];
            }

            set
            {
                int fromInt = States.IndexOf(from);
                int toInt = States.IndexOf(to);

                if (fromInt < 0)
                {
                    throw new ArgumentException("Character " + from + " is not found in the rate matrix!", nameof(from));
                }

                if (toInt < 0)
                {
                    throw new ArgumentException("Character " + to + " is not found in the rate matrix!", nameof(to));
                }

                this[fromInt, toInt] = value;
            }
        }

        /// <inheritdoc/>
        protected internal sealed override double GetThis(int from, int to) => this[from, to];

        /// <inheritdoc/>
        protected internal sealed override double GetThis(char from, char to) => this[from, to];

        /// <summary>
        /// Gets the equilibrium frequences of the rate matrix.
        /// </summary>
        public override ImmutableArray<double> EquilibriumFrequencies
        {
            get
            {
                if (this.equilibriumFrequencies == null)
                {
                    Matrix<double> rateMatrix = Matrix<double>.Build.DenseOfArray(rates);

                    for (int i = 0; i < States.Length; i++)
                    {
                        rateMatrix[i, i] = this[i, i];
                    }

                    Vector<double>[] kernel = rateMatrix.Transpose().Kernel();

                    if (kernel == null || kernel.Length == 0)
                    {
                        throw new ArgumentException("The kernel of the transpose of the rate matrix is empty!");
                    }

                    Vector<double> equilibriumFrequencies = kernel[0] / kernel[0].Sum();

                    this.equilibriumFrequencies = equilibriumFrequencies.ToArray();
                }

                return ImmutableArray.Create<double>(equilibriumFrequencies);
            }
        }

        /// <summary>
        /// Creates a new <see cref="MutableRateMatrix"/> with the specified <paramref name="states"/>.
        /// </summary>
        /// <param name="states">The possible states of the character described by the <see cref="MutableRateMatrix"/>.</param>
        public MutableRateMatrix(ReadOnlySpan<char> states)
        {
            this.States = ImmutableArray.Create<char>(states);
            this.equilibriumFrequencies = null;
            this.rates = new double[states.Length, states.Length];
            this.exponential = null;
        }

        /// <summary>
        /// Creates a new <see cref="MutableRateMatrix"/> with the specified <paramref name="states"/> and <paramref name="rates"/>.
        /// </summary>
        /// <param name="states">The possible states of the character described by the <see cref="MutableRateMatrix"/>.</param>
        /// <param name="rates">A 2D <see langword="double>"/> array containing the rates used to initialise the matrix.
        /// The number of rows and columns in the array must be equal to the number of states. Diagonal entries are ignored.</param>
        /// <exception cref="ArgumentException">Thrown if the number of rows or columns of the <paramref name="rates"/> matrix does
        /// not correspond to the number of <paramref name="states"/>.</exception>
        public MutableRateMatrix(ReadOnlySpan<char> states, double[,] rates)
        {
            if (states.Length != rates.GetLength(0) || states.Length != rates.GetLength(1))
            {
                throw new ArgumentException("The size of the rate matrix does not correspond to the number of states!");
            }

            this.States = ImmutableArray.Create<char>(states);
            this.equilibriumFrequencies = null;
            this.rates = new double[states.Length, states.Length];
            this.exponential = null;

            for (int i = 0; i < states.Length; i++)
            {
                for (int j = 0; j < states.Length; j++)
                {
                    if (i != j)
                    {
                        this.rates[i, j] = rates[i, j];
                    }
                }
            }
        }

        internal override Matrix<double> GetMatrix()
        {
            Matrix<double> tbr = Matrix<double>.Build.DenseOfArray(this.rates);

            for (int i= 0; i < this.States.Length; i++)
            {
                tbr[i, i] = this[i, i];
            }

            return tbr;
        }
        internal override double[] GetEquilibriumFrequencies()
        {
            return this.EquilibriumFrequencies.ToArray();
        }
        internal override char[] GetStates()
        {
            return this.States.ToArray();
        }

        internal override MatrixExponential GetExponential()
        {
            if (this.exponential == null)
            {
                this.exponential = this.GetMatrix().FastExponential(1);
            }

            return this.exponential;
        }
    }

    /// <summary>
    /// Represents a rate matrix whose values cannot be changed after initialisation.
    /// </summary>
    public class ImmutableRateMatrix : RateMatrix
    {
        /// <summary>
        /// Gets the states for the character to which the rate matrix applies.
        /// </summary>
        public override ImmutableArray<char> States { get; }

        /// <summary>
        /// Gets the equilibrium frequences of the rate matrix.
        /// </summary>
        public override ImmutableArray<double> EquilibriumFrequencies { get; }

        private readonly Matrix<double> rates;
        private readonly MatrixExponential exponential;

        /// <summary>
        /// Gets the rate of going from state number <paramref name="from"/> to state number
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row number.</param>
        /// <param name="to">The column number.</param>
        /// <returns>The rate of going from state number <paramref name="from"/> to state number <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state index is &lt; 0 or greater than the
        /// number of states in the rate matrix.</exception>
        public override double this[int from, int to]
        {
            get
            {
                if (from >= 0 && from < States.Length)
                {
                    if (to >= 0 && to < States.Length)
                    {
                        return rates[from, to];
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(to));
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(from));
                }
            }
        }

        /// <summary>
        /// Gets the rate of going from state <paramref name="from"/> to state
        /// <paramref name="to"/>. If <c><paramref name="from"/> == <paramref name="to"/></c>, the negative
        /// sum of the elements on the row is returned.
        /// </summary>
        /// <param name="from">The row state.</param>
        /// <param name="to">The column state.</param>
        /// <returns>The rate of going from state <paramref name="from"/> to state <paramref name="to"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the state is not part of the rate matrix.</exception>
        public override double this[char from, char to]
        {
            get
            {
                int fromInt = States.IndexOf(from);
                int toInt = States.IndexOf(to);

                if (fromInt < 0)
                {
                    throw new ArgumentException("Character " + from + " is not found in the rate matrix!", nameof(from));
                }

                if (toInt < 0)
                {
                    throw new ArgumentException("Character " + to + " is not found in the rate matrix!", nameof(to));
                }

                return this[fromInt, toInt];
            }
        }

        /// <summary>
        /// Creates a new <see cref="ImmutableRateMatrix"/> with the specified <paramref name="states"/> and <paramref name="rates"/>.
        /// </summary>
        /// <param name="states">The possible states of the character described by the <see cref="ImmutableRateMatrix"/>.</param>
        /// <param name="rates">A 2D <see langword="double>"/> array containing the rates used to initialise the matrix.
        /// The number of rows and columns in the array must be equal to the number of states. Diagonal entries are ignored.</param>
        /// <exception cref="ArgumentException">Thrown if the number of rows or columns of the <paramref name="rates"/> matrix does
        /// not correspond to the number of <paramref name="states"/>.</exception>
        public ImmutableRateMatrix(ReadOnlySpan<char> states, double[,] rates)
        {
            if (states.Length != rates.GetLength(0) || states.Length != rates.GetLength(1))
            {
                throw new ArgumentException("The size of the rate matrix does not correspond to the number of states!");
            }

            this.States = ImmutableArray.Create<char>(states);


            this.rates = Matrix<double>.Build.Dense(states.Length, states.Length);

            for (int i = 0; i < states.Length; i++)
            {
                double diag = 0;

                for (int j = 0; j < states.Length; j++)
                {
                    if (i != j)
                    {
                        this.rates[i, j] = rates[i, j];
                        diag += rates[i, j];
                    }
                }

                this.rates[i, i] = -diag;
            }

            Vector<double>[] kernel = this.rates.Transpose().Kernel();

            if (kernel == null || kernel.Length == 0)
            {
                throw new ArgumentException("The kernel of the transpose of the rate matrix is empty!");
            }

            Vector<double> equilibriumFrequencies = kernel[0] / kernel[0].Sum();

            this.EquilibriumFrequencies = ImmutableArray.Create<double>(equilibriumFrequencies.ToArray());
            this.exponential = this.rates.FastExponential(1);
        }

        /// <summary>
        /// Creates a new <see cref="ImmutableRateMatrix"/> with the specified <paramref name="states"/> and <paramref name="rates"/>.
        /// </summary>
        /// <param name="states">The possible states of the character described by the <see cref="ImmutableRateMatrix"/>.</param>
        /// <param name="rates">A 2D <see langword="double>"/> array containing the rates used to initialise the matrix.
        /// The number of rows and columns in the array must be equal to the number of states. Diagonal entries are ignored.</param>
        /// <param name="equilibriumFrequencies">Equilibrium frequencies for the rate matrix. These are not checked, so they better
        /// be correct!</param>
        /// <exception cref="ArgumentException">Thrown if the number of rows or columns of the <paramref name="rates"/> matrix, or the
        /// number of equilibrium frequencies, does not correspond to the number of <paramref name="states"/>.</exception>
        /// <remarks>Using this constructor is faster than the <see cref="ImmutableRateMatrix(ReadOnlySpan{char}, double[,])"/> constructor,
        /// as equilbrium frequencies are not computed. This is especially useful for pre-baked rate matrices.</remarks>
        public ImmutableRateMatrix(ReadOnlySpan<char> states, double[,] rates, double[] equilibriumFrequencies)
        {
            if (states.Length != rates.GetLength(0) || states.Length != rates.GetLength(1))
            {
                throw new ArgumentException("The size of the rate matrix does not correspond to the number of states!");
            }

            if (states.Length != equilibriumFrequencies.Length)
            {
                throw new ArgumentException("The number of equilibrium frequencies does not correspond to the number of states!");
            }

            this.States = ImmutableArray.Create<char>(states);


            this.rates = Matrix<double>.Build.Dense(states.Length, states.Length);

            for (int i = 0; i < states.Length; i++)
            {
                double diag = 0;

                for (int j = 0; j < states.Length; j++)
                {
                    if (i != j)
                    {
                        this.rates[i, j] = rates[i, j];
                        diag += rates[i, j];
                    }
                }

                this.rates[i, i] = -diag;
            }

            this.EquilibriumFrequencies = ImmutableArray.Create<double>(equilibriumFrequencies);
            this.exponential = this.rates.FastExponential(1);
        }

        internal override Matrix<double> GetMatrix()
        {
            return this.rates.Clone();
        }
        internal override double[] GetEquilibriumFrequencies()
        {
            return this.EquilibriumFrequencies.ToArray();
        }
        internal override char[] GetStates()
        {
            return this.States.ToArray();
        }
        internal override MatrixExponential GetExponential()
        {
            return this.exponential;
        }
    }
}
