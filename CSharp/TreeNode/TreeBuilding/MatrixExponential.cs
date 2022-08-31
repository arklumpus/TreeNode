using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace PhyloTree.TreeBuilding
{
    internal class MatrixExponential
    {
        public Matrix<double> Result;
        public bool Exact;
        public Matrix<Complex> P;
        public Matrix<Complex> PInv;
        public Matrix<Complex> D;

        public MatrixExponential(Matrix<double> result, Matrix<Complex> p, Matrix<Complex> pInv, Matrix<Complex> d)
        {
            Result = result;
            P = p;
            PInv = pInv;
            Exact = p != null;
            D = d;
        }
    }

    internal static class MatrixExtensions
    {
        static bool ForcePade = false;

        public static MatrixExponential FastExponential(this Matrix<double> mat, double t, MatrixExponential cachedResult = null)
        {
            if (ForcePade)
            {
                return new MatrixExponential((mat * t).PadeExponential().PointwiseAbs(), null, null, null);
            }

            if (cachedResult == null)
            {
                try
                {
                    Matrix<Complex> m = mat.ToComplex();
                    Evd<Complex> evd = m.Evd();

                    HashSet<Complex> eigenValues = new HashSet<Complex>();

                    for (int i = 0; i < evd.EigenValues.Count; i++)
                    {
                        if (Math.Abs(evd.EigenValues[i].Real) < 1e-5)
                        {
                            evd.EigenValues[i] = new Complex(0, evd.EigenValues[i].Imaginary);
                        }

                        if (Math.Abs(evd.EigenValues[i].Imaginary) < 1e-5)
                        {
                            evd.EigenValues[i] = new Complex(evd.EigenValues[i].Real, 0);
                        }

                        eigenValues.Add(evd.EigenValues[i]);
                    }

                    if (eigenValues.Count == m.ColumnCount)
                    {
                        //Diagonalizable

                        Matrix<Complex> inv = evd.EigenVectors.Inverse();
                        Matrix<Complex> eig = evd.EigenVectors;
                        Matrix<Complex> diag = Matrix<Complex>.Build.DenseOfDiagonalVector(evd.EigenValues);

                        return new MatrixExponential((eig * (diag * t).DiagonalExp() * inv).Real().PointwiseAbs(), eig, inv, diag);
                    }
                    else
                    {
                        //Might not diagonalizable: fallback to Padé approximation [note: this happens "almost never"]
                        return new MatrixExponential((mat * t).PadeExponential().PointwiseAbs(), null, null, null);
                    }
                }
                catch
                {
                    //Error during diagonalization: fallback to Padé approximation
                    return new MatrixExponential((mat * t).PadeExponential().PointwiseAbs(), null, null, null);
                }
            }
            else
            {
                if (cachedResult.Exact)
                {
                    return new MatrixExponential((cachedResult.P * (cachedResult.D * t).DiagonalExp() * cachedResult.PInv).Real().PointwiseAbs(), cachedResult.P, cachedResult.PInv, cachedResult.D);
                }
                else
                {
                    return new MatrixExponential((mat * t).PadeExponential().PointwiseAbs(), null, null, null);
                }
            }
        }

        static Matrix<Complex> DiagonalExp(this Matrix<Complex> m)
        {
            Matrix<Complex> tbr = Matrix<Complex>.Build.DenseOfMatrix(m);

            for (int i = 0; i < m.ColumnCount; i++)
            {
                tbr[i, i] = tbr[i, i].Exp();
            }

            return tbr;
        }


        public static void TimesLogVectorAndAdd(this Matrix<double> mat, double[] logVector, double[] addToVector)
        {
            int maxInd = logVector.MaxInd();

            for (int i = 0; i < mat.RowCount; i++)
            {
                double toBeAdded = logVector[maxInd] + Math.Log(mat[i, maxInd]);

                double log1pArg = 0;

                for (var j = 0; j < mat.ColumnCount; j++)
                {
                    if (j != maxInd)
                    {
                        log1pArg += mat[i, j] / mat[i, maxInd] * Math.Exp(logVector[j] - logVector[maxInd]);
                    }
                }

                if (!double.IsNaN(log1pArg))
                {
                    toBeAdded += Log1p(log1pArg);
                    addToVector[i] += toBeAdded;
                    if (addToVector[i] > 0)
                    {
                        addToVector[i] = double.NaN;
                    }
                }
                else
                {
                    double logArg = 0;
                    for (var j = 0; j < mat.ColumnCount; j++)
                    {
                        logArg += mat[i, j] * Math.Exp(logVector[j]);
                    }

                    addToVector[i] += Math.Log(logArg);

                    if (addToVector[i] > 0)
                    {
                        addToVector[i] = double.NaN;
                    }
                }
            }
        }

        private static int MaxInd(this double[] arr)
        {
            int tbr = 0;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] > arr[tbr])
                {
                    tbr = i;
                }
            }

            return tbr;
        }

        private static double Log1p(double x)
        {
            if (x <= -1)
            {
                return double.NegativeInfinity;
            }
            else if (Math.Abs(x) > 0.0001)
            {
                return Math.Log(1 + x);
            }
            else
            {
                return (1 - 0.5 * x) * x;
            }
        }

        //Adapted from https://github.com/horribleheffalump/MatrixExponential/blob/master/MatrixExponential.cs
        public static Matrix<double> PadeExponential(this Matrix<double> m)
        {
            Matrix<double> exp_m = null;

            // last hope: Padé approximation method
            // details could be found in 
            // M.Arioli, B.Codenotti, C.Fassino The Padé method for computing the matrix exponential // Linear Algebra and its Applications, 1996, V. 240, P. 111-130
            // https://www.sciencedirect.com/science/article/pii/0024379594001901

            //Assume that matrix is not diagonalizable (otherwise, FastExponential would have succeded

            int p = 5; // order of Padé 

            // high matrix norm may result in high roundoff erroros, 
            // so first we have to find normalizing coefficient such that || m / norm_coeff || < 0.5
            // to reduce the following computations we set it norm_coeff = 2^k

            double k = 0;
            double mNorm = m.L1Norm();
            if (mNorm > 0.5)
            {
                k = Math.Ceiling(Math.Log(mNorm) / Math.Log(2.0));
                m = m / Math.Pow(2.0, k);
            }

            Matrix<double> N = DenseMatrix.CreateIdentity(m.RowCount);
            Matrix<double> D = DenseMatrix.CreateIdentity(m.RowCount);
            Matrix<double> m_pow_j = m;

            int q = p; // here we use simmetric approximation, but in general p may not be equal to q.
            for (int j = 1; j <= Math.Max(p, q); j++)
            {
                if (j > 1)
                    m_pow_j = m_pow_j * m;
                if (j <= p)
                    N = N + SpecialFunctions.Factorial(p + q - j) * SpecialFunctions.Factorial(p) / SpecialFunctions.Factorial(p + q) / SpecialFunctions.Factorial(j) / SpecialFunctions.Factorial(p - j) * m_pow_j;
                if (j <= q)
                    D = D + Math.Pow(-1.0, j) * SpecialFunctions.Factorial(p + q - j) * SpecialFunctions.Factorial(q) / SpecialFunctions.Factorial(p + q) / SpecialFunctions.Factorial(j) / SpecialFunctions.Factorial(q - j) * m_pow_j;
            }

            // calculate inv(D)*N with LU decomposition
            exp_m = D.LU().Solve(N);

            // denormalize if need
            if (k > 0)
            {
                for (int i = 0; i < k; i++)
                {
                    exp_m = exp_m * exp_m;
                }
            }

            return exp_m;
        }
    }
}
