using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Diagnostics;

//potrebne kniznice OpenTK
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;


namespace MFSpheres
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            IsRightDown = false;
        }

//-----------------------------------------------------------------------------------------------------------------------

        #region GLOBALNE PREMENNE

        /////////////////////////////////////////////////////
        //                                                 //
        //                 GLOBALNE PREMENNE               //
        //                                                 //
        /////////////////////////////////////////////////////

        GLControl glControl;

        float eps = 0.00001f;

        // parametre na ovladanie kamery
        float Dist, Phi, Theta, dPhi, dTheta;

        // parametre mysi
        float RightX, RightY;
        bool IsRightDown;

        // kliknuta sfera
        int ActiveSphere = -1, HitIndex = -1;

        // kliknuty bod
        Vector3 HitPoint = new Vector3();

        // zoznam sfer ktore modifikuje pouzivatel
        List<Sphere> Spheres = new List<Sphere>();

        // hodnoty pouzite vo vahovych funkciach
        int Eta;
        float Delta;

        // ked budeme vytvarat novu sferu
        bool CreatingSphere = false;

        // ked chceme zobrazit potahovu plochu
        bool ShowSurface = false;

        // vzorkovacie body na jednotlivych segmentoch potahovej plochy
        int Lod1, Lod2;
        List<Vector3[,,]> SamplesUpDown = new List<Vector3[,,]>();
        List<Vector3[,]> Samples = new List<Vector3[,]>();

        // normalove vektory rovin, v ktorych lezia dotykove kruznice, pre tubularnu konstrukciu
        List<Vector3> Normals = new List<Vector3>();

        // index kliknuteho vektora ked ho budeme menit
        int HitNormal = -1;

        // ked chceme zobrazit tubularnu plochu
        bool ShowTubular = false;

        float Tau = 1.0f, Lambda = 1.0f;
        public static float casT = 1.0f;
        public static bool Homotopy = false;
        public static float alpha = 1.0f;
        public static bool ColorOne = true;

        float casRozv = 0.0f, casHom = 0.0f;

        // referencny bod a polomer
        Vector3 Pref;
        float rRef;

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region INICIALIZACIA OpenTK, SCENY A PARAMETROV

        private void WindowsFormsHost_Initialized(object sender, EventArgs e)
        {
            // inicializacia OpenTK;
            OpenTK.Toolkit.Init();
            var flags = GraphicsContextFlags.Default;
            glControl = new GLControl(new GraphicsMode(32, 24), 2, 0, flags);
            glControl.MakeCurrent();
            glControl.Paint += GLControl_Paint;
            glControl.Dock = DockStyle.Fill;
            (sender as WindowsFormsHost).Child = glControl;

            // vymenovanie ovladacich procedur
            glControl.MouseDown += GLControl_MouseDown;
            glControl.MouseMove += GLControl_MouseMove;
            glControl.MouseUp += GLControl_MouseUp;
            glControl.MouseWheel += GLControl_MouseWheel;
            glControl.MouseDoubleClick += GLControl_MouseDoubleClick;

            // tienovanie
            GL.ShadeModel(ShadingModel.Smooth);

            // farba pozadia
            GL.ClearColor(0.25f, 0.25f, 0.25f, 1.0f);
            //GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);

            // cistiaca hodnata pre z-buffer
            GL.ClearDepth(1.0f);

            // zapne z-buffer, zmiesavanie a vyhladzovanie
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // vyhladzovanie
            GL.Enable(EnableCap.LineSmooth);
            GL.Enable(EnableCap.PointSmooth);

            // osvetlovanie
            float[] light_diffuse = { 0.4f, 0.4f, 0.4f, 1.0f };
            float[] light_ambient = { 0.3f, 0.3f, 0.3f, 1.0f };
            float[] light_specular = { 0.5f, 0.5f, 0.5f, 1.0f };
            float[] light_position = { 5.0f, 5.0f, 5.0f };
            GL.Light(LightName.Light0, LightParameter.Diffuse, light_diffuse);
            GL.Light(LightName.Light0, LightParameter.Ambient, light_ambient);
            GL.Light(LightName.Light0, LightParameter.Specular, light_specular);
            GL.Light(LightName.Light0, LightParameter.QuadraticAttenuation, 1.0f);
            GL.Light(LightName.Light0, LightParameter.Position, light_position);
            GL.Enable(EnableCap.Light0);

            // vstupne parametre kamery
            Phi = 0.0f; Theta = (float)Math.PI / 2.0f; Dist = 5.0f;

            // vstupne hodnoty do vahovych funkcii
            Eta = 2;
            Delta = 0.5f;

            // pocet vzorkovacich bodov na kazdy segment bocnej plochy
            Lod1 = 40;
            Lod2 = 8;
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCTY

        #region VYPOCTY PRE ROZVETVENU POTAHOVU PLOCHU 

        ////////////////////////////////////////////////////////////
        //                                                        //
        //         VYPOCTY PRE ROZVETVENU POTAHOVU PLOCHU         //
        //                                                        //
        ////////////////////////////////////////////////////////////

//-----------------------------------------------------------------------------------------------------------------------

        // otestuje ci je Delta vyhovujuca 
        private bool CheckDelta()
        {
            for (int i = 0; i < Spheres.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    if (Delta >= (Spheres[j].Center.Xy - Spheres[i].Center.Xy).Length / (Spheres[j].R + Spheres[i].R)) return false;
                }                        
                for (int j = i + 1; j < Spheres.Count; j++)
                {
                    if (Delta >= (Spheres[j].Center.Xy - Spheres[i].Center.Xy).Length / (Spheres[j].R + Spheres[i].R)) return false;
                }
            }
            return true;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // najde maximalne moznu hodnotu Delty
        private float FindMaxDelta()
        {
            float d = 1.0f;
            for (int i = 0; i < Spheres.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    d = Math.Min(d, (Spheres[j].Center.Xy - Spheres[i].Center.Xy).Length / (Spheres[j].R + Spheres[i].R) - 0.000001f);
                }
                for (int j = i + 1; j < Spheres.Count; j++)
                {
                    d = Math.Min(d, (Spheres[j].Center.Xy - Spheres[i].Center.Xy).Length / (Spheres[j].R + Spheres[i].R) - 0.000001f);
                }
            }
            return d;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // Kroneckerova delta funkcia
        private int KDelta(int i, int j)
        {
            if (i == j) return 1;
            else return 0;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // i-ta vahova funkcia v bode X pri danej mnozine sfer S
        private float w(int i, Vector2 X, List<Sphere> S, float delta)
        {
            for (int j = 0; j < S.Count; j++)
            {
                if ((X - S[j].Center.Xy).Length - delta* S[j].R <= eps) return KDelta(i, j); //if (Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2) <= eps) return KDelta(i, j);
            }

            float wSum = 0.0f;
            for (int j = 0; j < S.Count; j++)
            {
                wSum += p(j, X, S, delta); //* (float)Math.Pow((P(X, S)- S[j].Center).Length, n));
            }
            return p(i, X, S, delta) / wSum; //* (float)Math.Pow((P(X, S) - S[i].Center).Length, n))) / wSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // funkcia v citateli i-tej vahovej funkcie, ktora sluzi na stabilnejsie vyjadrenie vahovych funkcii
        private float p(int i, Vector2 X, List<Sphere> S, float delta)
        {
            float pSum = 1.0f;
            for (int j = 0; j < i; j++)
            {
                pSum *= (float)Math.Pow((X - S[j].Center.Xy).Length - delta * S[j].R, Eta); //(float)Math.Pow(Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2), n);
            }
            for (int j = i + 1; j < S.Count; j++)
            {
                pSum *= (float)Math.Pow((X - S[j].Center.Xy).Length - delta * S[j].R, Eta); //(float)Math.Pow(Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2), n);
            }
            return pSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // rozdiel polomeru i-tej sfery a referencnej (prvej sfery)
        private float dR(int i, List<Sphere> S)
        {
            return S[i].R - S[0].R;
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // funkcia ktora interpoluje zmeny dR
        private float R(Vector2 X, List<Sphere> S)
        {
            float rSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                rSum += w(i, X, S, Delta) * dR(i, S);
            }
            return rSum;
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // parametrizacia roviny paralelnej s rovinou z = 0, ktora prechadza referencnym bodom S[0].Center + S[0].R * n, n=(0,0,1) 
        private Vector3 P(Vector2 X, List<Sphere> S)
        {
            /* Vector3 n = new Vector3(0.0f, 0.0f, 1.0f);
             Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
             Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);
             Vector3 pref = S[0].Center + S[0].R * n;*/
            return new Vector3(X.X, X.Y, S[0].R); //pref + (X.X - S[0].Center.X) * e1 + (X.Y - S[0].Center.Y) * e2;
        }
        
//-----------------------------------------------------------------------------------------------------------------------
        
        // vypocita bod na vrchnej potahovej ploche sfer S v bode X
        private Vector3 SurfacePoint(Vector2 X, List<Sphere> S)
        {
            Vector3 n = new Vector3(0.0f, 0.0f, 1.0f);
            return P(X, S) + R(X, S) * n;
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // najde priesecnik luca, ktory ziskame inverznou transformaciou bodu kde sme klikli mysou, so sferami v scene  
        private Vector3 FindIntersection(List<Sphere> S, Vector2 mouse)
        {
            float t = float.MaxValue;
            float tP = t;

            Vector3 A, B;

            int[] viewport = new int[4];
            Matrix4 modelMatrix, projMatrix;

            GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
            GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
            GL.GetInteger(GetPName.Viewport, viewport);

            A = UnProject(new Vector3(mouse.X, mouse.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
            B = UnProject(new Vector3(mouse.X, mouse.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

            Vector3 v = (B - A).Normalized();

            float a = v.Length * v.Length;

            for (int i = 0; i < S.Count; i++)
            {
                float b = 2 * Vector3.Dot(v, A - S[i].Center);
                float c = Vector3.Dot(A - S[i].Center, A - S[i].Center) - S[i].R * S[i].R; //(eye - S[i].Center).Length * (eye - S[i].Center).Length
                float D = b * b - 4 * a * c;

                if (D > 0) tP = (float)Math.Min((-b + Math.Sqrt(D)) / (2 * a), (-b - Math.Sqrt(D)) / (2 * a));
                else if (D == 0) tP = -b / (2 * a);

                if (tP < t)
                {
                    t = tP;
                    HitIndex = i;
                }
            }
            if (t == float.MaxValue) return Vector3.Zero;
            else return A + t * v;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // rotacia, pomocou kvaternionov, bodu X o uhol theta okolo osi, ktora spaja stredy sfer S0 a S1
        private Vector3 M(Vector3 X, float theta, Sphere S0, Sphere S1)
        {
            Vector3 X2 = X - S0.Center;
            Vector3 v = (S1.Center - S0.Center).Normalized();
            v = (float)Math.Sin(theta / 2.0f) * v;
            Vector4 q = new Vector4(v, (float)Math.Cos(theta / 2.0f));

            Vector3 row0 = new Vector3(1 - 2 * (q.Y * q.Y + q.Z * q.Z), 2 * (q.X * q.Y - q.W * q.Z), 2 * (q.X * q.Z + q.W * q.Y));
            Vector3 row1 = new Vector3(2 * (q.X * q.Y + q.Z * q.W), 1 - 2 * (q.X * q.X + q.Z * q.Z), 2 * (q.Y * q.Z - q.X * q.W));
            Vector3 row2 = new Vector3(2 * (q.X * q.Z - q.Y * q.W), 2 * (q.Y * q.Z + q.X * q.W), 1 - 2 * (q.X * q.X + q.Y * q.Y));

            Matrix3 Mat = new Matrix3(row0, row1, row2);
            X2 = Mat * X2;
            X2 = X2 + S0.Center;

            return X2;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // rotacia, pomocou kvaternionov, bodu X o uhol theta okolo osi, ktora je dana bodom S a smerovym vektorom v
        private Vector3 M(Vector3 X, float theta, Vector3 S, Vector3 v)
        {
            Vector3 X2 = X - S;
            Vector3 s = v.Normalized();
            s = (float)Math.Sin(theta / 2.0f) * s;
            Vector4 q = new Vector4(s, (float)Math.Cos(theta / 2.0f));

            Vector3 row0 = new Vector3(1 - 2 * (q.Y * q.Y + q.Z * q.Z), 2 * (q.X * q.Y - q.W * q.Z), 2 * (q.X * q.Z + q.W * q.Y));
            Vector3 row1 = new Vector3(2 * (q.X * q.Y + q.Z * q.W), 1 - 2 * (q.X * q.X + q.Z * q.Z), 2 * (q.Y * q.Z - q.X * q.W));
            Vector3 row2 = new Vector3(2 * (q.X * q.Z - q.Y * q.W), 2 * (q.Y * q.Z + q.X * q.W), 1 - 2 * (q.X * q.X + q.Y * q.Y));

            Matrix3 Mat = new Matrix3(row0, row1, row2);
            X2 = Mat * X2;
            X2 = X2 + S;

            return X2;
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET COONSOVEJ BIKUBICKY STMELOVANEJ ZAPLATY SEGMENTU BOCNEJ PLOCHY

        //////////////////////////////////////////////////////////////////////////////////////
        //                                                                                  //
        //      VYPOCET COONSOVEJ BIKUBICKY STMELOVANEJ ZAPLATY SEGMENTU BOCNEJ PLOCHY      //
        //                                                                                  //
        //////////////////////////////////////////////////////////////////////////////////////

//-----------------------------------------------------------------------------------------------------------------------

        // Hermitove kubicke polynomy
        private float H3(int i, float x)
        {
            float t = 0.0f;
            switch (i)
            {
                case 0:
                    t = 2 * x * x * x - 3 * x * x + 1;
                    break;
                case 1:
                    t = x * x * x - 2 * x * x + x;
                    break;
                case 2:
                    t = x * x * x - x * x;
                    break;
                case 3:
                    t = -2 * x * x * x + 3 * x * x;
                    break;
                default:
                    t = 0.0f;
                    break;
            }
            return t;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // derivacia Hermitovych kubickych polynomov
        private float dH3(int i, float x)
        {
            float t = 0.0f;
            switch (i)
            {
                case 0:
                    t = 6 * x * x - 6 * x;
                    break;
                case 1:
                    t = 3 * x * x - 4 * x + 1;
                    break;
                case 2:
                    t = 3 * x * x - 2 * x;
                    break;
                case 3:
                    t = -6 * x * x + 6 * x;
                    break;
                default:
                    t = 0.0f;
                    break;
            }
            return t;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private List<Vector3> CheckKonvexAt(int i, int j, List<Sphere> S)
        {
            Vector3 t0 = new Vector3(), t1 = new Vector3();
            if (S.Count > 1)
            {
                if (i >= 1 && i <= S.Count - 3)
                {
                    t0 = S[i + j].Center - S[i + j - 1].Center;
                    t1 = S[i + j + 1].Center - S[i + j].Center;
                }
                else if (i == 0)
                {
                    if (j == 0)
                    {
                        t0 = S[0].Center - S[S.Count - 1].Center;
                        t1 = S[1].Center - S[0].Center;
                    }
                    else
                    {
                        t0 = S[1].Center - S[0].Center;
                        if (S.Count > 2) t1 = S[2].Center - S[1].Center;
                        else t1 = S[0].Center - S[1].Center;
                    }
                }
                else if (i == S.Count - 2)
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 2].Center - S[S.Count - 3].Center;
                        t1 = S[S.Count - 1].Center - S[S.Count - 2].Center;
                    }
                    else
                    {
                        t0 = S[S.Count - 1].Center - S[S.Count - 2].Center;
                        t1 = S[0].Center - S[S.Count - 1].Center;
                    }
                }
                else
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 1].Center - S[S.Count - 2].Center;
                        t1 = S[0].Center - S[S.Count - 1].Center;
                    }
                    else
                    {
                        t0 = S[0].Center - S[S.Count - 1].Center;
                        t1 = S[1].Center - S[0].Center;
                    }
                }

                if (det(t0.Xy, t1.Xy) < -eps)
                {
                    Vector3 s = (t0 + t1) / 2.0f;
                    t0 = s;
                    t1 = s;
                }
            }
            return new List<Vector3> { t0, t1 };
        }

//-----------------------------------------------------------------------------------------------------------------------

        private Vector3 HermiteCurve(float u, Vector3 A, Vector3 v0, Vector3 v1, Vector3 B)
        {
            return H3(0, u) * A + H3(1, u) * v0 + H3(2, u) * v1 + H3(3, u) * B;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private Vector3 dHermiteCurve(float u, Vector3 A, Vector3 v0, Vector3 v1, Vector3 B)
        {
            return dH3(0, u) * A + dH3(1, u) * v0 + dH3(2, u) * v1 + dH3(3, u) * B;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka c0 Coonsovej zaplaty
        private Vector3 c0(int i, float u, List<Sphere> S)
        {
            Vector3 X = c1(i, u, S);
            X.Z = -X.Z;
            return X;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka c1 Coonsovej zaplaty na i-tom segmente
        private Vector3 c1(int i, float u, List<Sphere> S)
        {
            Vector3 X;
            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[i + 1].Center);  //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
            else X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[0].Center);  //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
            return SurfacePoint(X.Xy, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d0 Coonsovej zaplaty na i-tom segmente
        private Vector3 d0(int i, float v, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].Center - S[i].Center;
            else ti3 = S[0].Center - S[i].Center;
            n = ti3.Xy;

            if (i > 0) s = S[i].Center.Xy - S[i - 1].Center.Xy;
            else s = S[i].Center.Xy - S[S.Count - 1].Center.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }
            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();
            return S[i].Center + S[i].R * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - S[i].R * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;

            /*if (i < Spheres.Count - 1) return M(X, -v * (float)Math.PI, Spheres[i], Spheres[i + 1]);
            else return M(X, -v *(float)Math.PI, Spheres[i], Spheres[0]);*/
        }

        // derivacia hranicnej krivky d0 Coonsovej zaplaty na i-tom segmente
        private Vector3 dd0(int i, float v, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].Center - S[i].Center;
            else ti3 = S[0].Center - S[i].Center;
            n = ti3.Xy;

            if (i > 0) s = S[i].Center.Xy - S[i - 1].Center.Xy;
            else s = S[i].Center.Xy - S[S.Count - 1].Center.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            return -S[i].R * (float)(Math.PI * Math.Sin(Math.PI * ((double)v + 1))) * e3 - S[i].R * (float)(Math.PI * Math.Cos(Math.PI * ((double)v + 1))) * ti2;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d1 Coonsovej zaplaty na i-tom segmente
        private Vector3 d1(int i, float v, List<Sphere> S)
        {
            //Vector3 X;
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Sphere C1;
            Vector2 s, n;
            Vector3 ti3;

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Center, S[0].R);
            s = C1.Center.Xy - S[i].Center.Xy;

            if (i < S.Count - 2) ti3 = S[i + 2].Center - S[i + 1].Center;
            else if (i == S.Count - 2) ti3 = S[0].Center - S[i + 1].Center;
            else ti3 = S[1].Center - S[0].Center;

            n = ti3.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }
            else ti3 = new Vector3(s.X, s.Y, 0.0f);

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            return C1.Center + C1.R * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - C1.R * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;

            /*if (i < Spheres.Count - 1)
            {
                X = Spheres[i + 1].Center + Spheres[i + 1].R * new Vector3(0.0f, 0.0f, -1.0f);
                return M(X, -v * (float)Math.PI, Spheres[i], Spheres[i + 1]);
            }
            else
            {
                X = Spheres[0].Center + Spheres[0].R * new Vector3(0.0f, 0.0f, -1.0f);
                return M(X, -v * (float)Math.PI, Spheres[i], Spheres[0]);
            }*/
        }
     
        // derivacia hranicnej krivky d1 Coonsovej zaplaty na i-tom segmente
        private Vector3 dd1(int i, float v, List<Sphere> S)
        {
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Sphere C1;
            Vector2 s, n;
            Vector3 ti3;

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Center, S[0].R);
            s = C1.Center.Xy - S[i].Center.Xy;

            if (i < S.Count - 2) ti3 = S[i + 2].Center - S[i + 1].Center;
            else if (i == S.Count - 2) ti3 = S[0].Center - S[i + 1].Center;
            else ti3 = S[1].Center - S[0].Center;

            n = ti3.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }
            else ti3 = new Vector3(s.X, s.Y, 0.0f);

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            return -C1.R * (float)(Math.PI * Math.Sin(Math.PI * ((double)v + 1))) * e3 - C1.R * (float)(Math.PI * Math.Cos(Math.PI * ((double)v + 1))) * ti2;
        }
       
//-----------------------------------------------------------------------------------------------------------------------

        // j-ta (j=0,1) funkcia derivacii ej na i-tom segmente bocnej potahovej plochy
        private Vector3 e(int i, int j, float u, List<Sphere> S)
        {
            Vector3 X; //v;
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);

            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[i + 1].Center);
            else X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[0].Center);

            /*if (i < Spheres.Count - 1)
            {
                //v = Vector3.Cross(Spheres[i + 1].Center - Spheres[i].Center, e3);
                v = Spheres[i + 1].Center - Spheres[i].Center;
                //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
                X = Spheres[i].Center + u * v;
            }
            else
            {
                //v = Vector3.Cross(Spheres[0].Center - Spheres[i].Center, e3);
                v = Spheres[0].Center - Spheres[i].Center;
                //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
                X = Spheres[i].Center + u * v;
            }*/

            //float tau = Lambda * v.Length;
            Vector3 nS;
            Vector3 dcij; 

            nS = normalS(X.X, X.Y, S);
            dcij = ds(i, u, S);

            if (j == 0)
            {
                nS.Z = -nS.Z;
                dcij.Z = -dcij.Z;
            }

            return Tau * Vector3.Cross(nS, dcij).Normalized();
            /* if (i < Spheres.Count - 1)
             {
                 v = Vector3.Cross(Spheres[i + 1].Center - Spheres[i].Center, e3);
                 X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
             }
             else
             {
                 v = Vector3.Cross(Spheres[0].Center - Spheres[i].Center, e3);
                 X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
             }

             float U = -(float)Math.Acos(Vector3.Dot(v, e1) / v.Length);

             Vector3 dd00 = new Vector3(Spheres[i].R * (float)Math.Cos(U), Spheres[i].R * (float)Math.Sin(U), 0.0f);
             Vector3 t00 = new Vector3(-Spheres[i].R * (float)Math.Sin(U), Spheres[i].R * (float)Math.Cos(U), 0.0f);
             Vector3 t01;
             Vector3 dd10;

             if (i < Spheres.Count - 1)
             {
                 t01 = new Vector3(-Spheres[i + 1].R * (float)Math.Sin(U), Spheres[i + 1].R * (float)Math.Cos(U), 0.0f);
                 dd10 = new Vector3(Spheres[i + 1].R * (float)Math.Cos(U), Spheres[i + 1].R * (float)Math.Sin(U), 0.0f);
             }
             else
             {
                 t01 = new Vector3(-Spheres[0].R * (float)Math.Sin(U), Spheres[0].R * (float)Math.Cos(U), 0.0f);
                 dd10 = new Vector3(Spheres[0].R * (float)Math.Cos(U), Spheres[0].R * (float)Math.Sin(U), 0.0f);
             }

             Vector3 dd01 = -dd00;
             Vector3 t10 = -t00;
             Vector3 t11 = -t01;
             Vector3 dd11 = -dd10;

             Vector3 d = ds(i, u, Spheres);
             Vector3 normal = normalS(X.X, X.Y, Spheres);
             //if(u > 0 && u < 1)
             // {
             switch (j)
                 {
                     case 0:
                         d.Z = -d.Z;
                         normal.Z = -normal.Z;
                         return (H3(0, u) * dd00 + H3(1, u) * t00 + H3(2, u) * t01 + H3(3, u) * dd10).Length * Vector3.Cross(normal, d).Normalized();
                     case 1:
                         return (H3(0, u) * dd01 + H3(1, u) * t10 + H3(2, u) * t11 + H3(3, u) * dd11).Length * Vector3.Cross(normal, d).Normalized();
                     default:
                         return Vector3.Zero;
                 }*/
            //}
            /*else if (u == 0)
            {
                switch (j)
                {
                    case 0:
                        return H3(0, u) * dd00 + H3(1, u) * t00 + H3(2, u) * t01 + H3(3, u) * dd10;
                    case 1:
                        return H3(0, u) * dd01 + H3(1, u) * t10 + H3(2, u) * t11 + H3(3, u) * dd11;
                    default:
                        return Vector3.Zero;
                }
            }
            else
            {
                switch (j)
                {
                    case 0:
                        return H3(0, u) * dd00 + H3(1, u) * t00 + H3(2, u) * t01 + H3(3, u) * dd10;
                    case 1:
                        return H3(0, u) * dd01 + H3(1, u) * t10 + H3(2, u) * t11 + H3(3, u) * dd11;
                    default:
                        return Vector3.Zero;
                }
            }*/

            /*switch (j)
            {
                case 0:
                    return H3(0, u) * dd00 + H3(1, u) * t00 + H3(2, u) * t01 + H3(3, u) * dd10;
                case 1:
                    return H3(0, u) * dd01 + H3(1, u) * t10 + H3(2, u) * t11 + H3(3, u) * dd11;
                default:
                    return Vector3.Zero;
            }*/
        }

//-----------------------------------------------------------------------------------------------------------------------

        // j-ta (j=0,1) funkcia derivacii fj na i-tom segmente bocnej potahovej plochy (aj v case t pre potahovu plochu pomocou homotopie)
        private Vector3 f(int i, int j, float v, List<Sphere> S)
        {
            Vector3 n;
            //float r1, r2;
            //r1 = (float)Math.Exp(Spheres[i].R);
            Sphere S0 = S[i], S1;
            Vector2 t0, t1, s;

            if (i < S.Count - 1)
            {
                S1 = S[i + 1];
                n = (S[i + 1].Center - S[i].Center).Normalized();
                //r2 = (float)Math.Exp(Spheres[i + 1].R);
            }
            else
            {
                S1 = S[0];
                n = (S[0].Center - S[i].Center).Normalized();
                //r2 = (float)Math.Exp(Spheres[0].R);
            }

            /*if (j == 0)
            {
                if (i > 0) t0 = Spheres[i].Center.Xy - Spheres[i - 1].Center.Xy;
                else t0 = Spheres[i].Center.Xy - Spheres[Spheres.Count - 1].Center.Xy;

                if (i < Spheres.Count - 1) t1 = Spheres[i + 1].Center.Xy - Spheres[i].Center.Xy;
                else t1 = Spheres[0].Center.Xy - Spheres[i].Center.Xy;
            }
            else
            {
                if (i < Spheres.Count - 1) t0 = Spheres[i + 1].Center.Xy - Spheres[i].Center.Xy;
                else t0 = Spheres[0].Center.Xy - Spheres[i].Center.Xy;

                if (i < Spheres.Count - 2) t1 = Spheres[i + 2].Center.Xy - Spheres[i + 1].Center.Xy;
                else if (i == Spheres.Count - 2) t1 = Spheres[0].Center.Xy - Spheres[i + 1].Center.Xy;
                else t1 = Spheres[1].Center.Xy - Spheres[0].Center.Xy;
            }*/
            if(S.Count > 2)
            {
                if (i >= 1 && i <= S.Count - 3)
                {
                    t0 = S[i + j].Center.Xy - S[i + j - 1].Center.Xy;
                    t1 = S[i + j + 1].Center.Xy - S[i + j].Center.Xy;
                }
                else if (i == 0)
                {
                    if (j == 0)
                    {
                        t0 = S[0].Center.Xy - S[S.Count - 1].Center.Xy;
                        t1 = S[1].Center.Xy - S[0].Center.Xy;
                    }
                    else
                    {
                        t0 = S[1].Center.Xy - S[0].Center.Xy;
                        t1 = S[2].Center.Xy - S[1].Center.Xy;
                    }
                }
                else if (i == S.Count - 2)
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 2].Center.Xy - S[S.Count - 3].Center.Xy;
                        t1 = S[S.Count - 1].Center.Xy - S[S.Count - 2].Center.Xy;
                    }
                    else
                    {
                        t0 = S[S.Count - 1].Center.Xy - S[S.Count - 2].Center.Xy;
                        t1 = S[0].Center.Xy - S[S.Count - 1].Center.Xy;
                    }
                }
                else
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 1].Center.Xy - S[S.Count - 2].Center.Xy;
                        t1 = S[0].Center.Xy - S[S.Count - 1].Center.Xy;
                    }
                    else
                    {
                        t0 = S[0].Center.Xy - S[S.Count - 1].Center.Xy;
                        t1 = S[1].Center.Xy - S[0].Center.Xy;
                    }
                }

                if (det(t0, t1) < -eps)
                {
                    s = (t0 + t1).Normalized();
                    n = new Vector3(s.X, s.Y, 0.0f);
                }
            }
            
            /*if (r1 < r2)
            {
                r2 = (float)Math.Exp(s.Length) * r2;
                r1 = (1.0f / (float)Math.Exp(s.Length)) * r1;
            }
            //if (r2 < 1) r2 = 1.0f / r2;

            switch (j)
            {
                case 0:
                    return Lambda * r1 * s;
                    //break;
                case 1:
                    return Lambda * r2 * s;
                    //break;
                default:
                    return s;
            }*/
            if (j == 0) return  (float)Math.Pow(S0.R / S1.R, Lambda) * n;
            else return (float)Math.Pow(S1.R / S0.R, Lambda) * n;


            /*Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 u;
            if (i < Spheres.Count - 1) u = Vector3.Cross(Spheres[i + 1].Center - Spheres[i].Center, e3);
            else u = Vector3.Cross(Spheres[0].Center - Spheres[i].Center, e3);
            float U = -(float)Math.Acos(Vector3.Dot(u, e1) / u.Length);

            Vector3 dc00 = Vector3.Cross(e(i, 0, 0), -Spheres[i].R * e1);
            Vector3 t00 = new Vector3(-Spheres[i].R * (float)Math.Sin(U), Spheres[i].R * (float)Math.Cos(U), 0.0f);
            Vector3 t10 = -t00;
            Vector3 dc10 = dc00;

            Vector3 dc01;
            Vector3 t01;
            */
            /*Vector3 d;
            float r1 = Delta * sphere[i].R;
            float r2;*/
            /*if (i < Spheres.Count - 1)
            {
                dc01 = Vector3.Cross(e(i, 0, 1), -Spheres[i + 1].R * e1);
                t01 = new Vector3(-Spheres[i + 1].R * (float)Math.Sin(U), Spheres[i + 1].R * (float)Math.Cos(U), 0.0f);
                /*d = (sphere[i + 1].Center - sphere[i].Center).Normalized();
                if (Delta != 0) r2 = Delta * sphere[i + 1].R;
                else r2 = sphere[i + 1].R;*/
            /* }
             else
             {
                 dc01 = Vector3.Cross(e(i, 0, 1), -Spheres[0].R * e1);
                 t01 = new Vector3(-Spheres[0].R * (float)Math.Sin(U), Spheres[0].R * (float)Math.Cos(U), 0.0f);
                 /*d = (sphere[0].Center - sphere[i].Center).Normalized();
                 if (Delta != 0) r2 = Delta * sphere[0].R;
                 else r2 = sphere[0].R;*/
            //}
            /* Vector3 t11 = -t01;
             Vector3 dc11 = dc01;

             switch (j)
             {
                 case 0:
                     return H3(0, v) * dc00 + H3(1, v) * t00 + H3(2, v) * t10 + H3(3, v) * dc10; //r1* d;
                 case 1:
                     return H3(0, v) * dc01 + H3(1, v) * t01 + H3(2, v) * t11 + H3(3, v) * dc11; //r2 * d;
                 default:
                     return Vector3.Zero;
             }*/
        }

//-----------------------------------------------------------------------------------------------------------------------

        // prva tvoriaca zaplata
        private Vector3 Sc(int i, float u, float v, List<Sphere> S)
        {
            return H3(0, v) * c0(i, u, S) + H3(1, v) * e(i, 0, u, S) + H3(2, v) * e(i, 1, u, S) + H3(3, v) * c1(i, u, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // druha tvoriaca zaplata
        private Vector3 Sd(int i, float u, float v, List<Sphere> S)
        {
            return H3(0, u) * d0(i, v, S) + H3(1, u) * f(i, 0, v, S) + H3(2, u) * f(i, 1, v, S) + H3(3, u) * d1(i, v, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // korekcna zaplata
        private Vector3 Scd(int i, float u, float v, List<Sphere> S)
        {
            /*Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 s = new Vector3();
            if (i < Spheres.Count - 1) s = Vector3.Cross(Spheres[i + 1].Center - Spheres[i].Center, e3);
            else s = Vector3.Cross(Spheres[0].Center - Spheres[i].Center, e3);
            float U = (float)Math.Acos(Vector3.Dot(s, e1) / s.Length);

            Vector3 t00 = new Vector3(-Spheres[i].R * (float)Math.Sin(U), Spheres[i].R * (float)Math.Cos(U), 0.0f);
            Vector3 t10 = -t00;
            Vector3 t01 = new Vector3();
            Vector3 t11 = new Vector3();

            if (i < Spheres.Count - 1) t01 = new Vector3(-Spheres[i + 1].R * (float)Math.Sin(U), Spheres[i + 1].R * (float)Math.Cos(U), 0.0f);
            else t01 = new Vector3(-Spheres[0].R * (float)Math.Sin(U), Spheres[0].R * (float)Math.Cos(U), 0.0f);
            t11 = -t01;
            */
            Vector4 Hu = new Vector4(H3(0, u), H3(1, u), H3(2, u), H3(3, u));
            Vector4 Hv = new Vector4(H3(0, v), H3(1, v), H3(2, v), H3(3, v));
            /*Vector3 t00 = Vector3.Zero;
            Vector3 t10 = Vector3.Zero;
            Vector3 t01 = Vector3.Zero;
            Vector3 t11 = Vector3.Zero;*/

            return Hv[0] * (Hu[0] * c0(i, 0, S) + Hu[1] * f(i, 0, 0, S) + Hu[2] * f(i, 1, 0, S) + Hu[3] * c0(i, 1, S)) +
                   Hv[1] * (Hu[0] * e(i, 0, 0, S) /*+ Hu[1] * t00 + Hu[2] * t01*/ + Hu[3] * e(i, 0, 1, S)) +
                   Hv[2] * (Hu[0] * e(i, 1, 0, S) /*+ Hu[1] * t10 + Hu[2] * t11*/ + Hu[3] * e(i, 1, 1, S)) +
                   Hv[3] * (Hu[0] * c1(i, 0, S) + Hu[1] * f(i, 0, 1, S) + Hu[2] * f(i, 1, 1, S) + Hu[3] * c1(i, 1, S));
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocita bod na Coonsovej zaplate na i-tom segmente
        private Vector3 CoonsPatchPoint(int i, float u, float v, List<Sphere> S)
        {
            return Sc(i, u ,v, S) + Sd(i, u, v, S) - Scd(i, u, v, S);
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET PARCIALNYCH DERIVACII HORNEJ POLOCHY 

        ////////////////////////////////////////////////////////////
        //                                                        //
        //      VYPOCET PARCIALNYCH DERIVACII HORNEJ POLOCHY      //
        //                                                        //
        ////////////////////////////////////////////////////////////

        //-----------------------------------------------------------------------------------------------------------------------

        private float F(int i, float u, float v, float delta, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            return 1.0f / (float)Math.Pow((X - S[i].Center.Xy).Length - delta * S[i].R, Eta);  //1.0f / (float)Math.Pow(Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) - Math.Pow(delta * S[i].R, 2), n);
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float dF(string uv, int i, float u, float v, float delta, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            if (uv == "u") return -Eta * (u - S[i].Center.X) / ((X - S[i].Center.Xy).Length * (float)Math.Pow((X - S[i].Center.Xy).Length - delta * S[i].R, Eta + 1));  //-2 * n * (u - S[i].Center.X) / (float)Math.Pow(Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) - Math.Pow(delta * S[i].R, 2), n + 1);   
            else if (uv == "v") return -Eta * (v - S[i].Center.Y) / ((X - S[i].Center.Xy).Length * (float)Math.Pow((X - S[i].Center.Xy).Length - delta * S[i].R, Eta + 1));  //-2 * n * (v - S[i].Center.Y) / (float)Math.Pow(Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) - Math.Pow(delta * S[i].R, 2), n + 1);  //
            else return 0.0f;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float G(float u, float v, float delta, List<Sphere> S)
        {
            float gSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                gSum += F(i, u, v, delta, S);
            }
            return gSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float dG(string uv, float u, float v, float delta, List<Sphere> S)
        {
            float gSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                gSum += dF(uv, i, u, v, delta, S);
            }
            return gSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // parcialna derivacia podla u/v i-tej vahovej funkcie
        private float dW(string uv, int i, float u, float v, float delta, List<Sphere> S)
        {
            return (dF(uv, i, u, v, delta, S) * G(u, v, delta, S) - F(i, u, v, delta, S) * dG(uv, u, v, delta, S)) / (float)Math.Pow(G(u, v, delta, S), 2);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // parcialna derivacia podla u/v parametrizacie hornej casti potahovej plochy
        private Vector3 dS(string uv, float u, float v, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);

            for (int i = 0; i < S.Count; i++)
            {
                if ((X - S[i].Center.Xy).Length - Delta * S[i].R <= eps && uv == "u") return e1;  //if (Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) - Math.Pow(Delta * S[i].R, 2) <= eps && uv == "u") return e1;
                if ((X - S[i].Center.Xy).Length - Delta * S[i].R <= eps && uv == "v") return e2;  //if (Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) - Math.Pow(Delta * S[i].R, 2) <= eps && uv == "v") return e2;
            }
            
            Vector3 nalfa = new Vector3(0.0f, 0.0f, 1.0f);
            float dr = 0.0f;

            for(int i = 0; i < S.Count; i++)
            {
                dr += dW(uv, i, u, v, Delta, S) * dR(i, S);
            }

            if (uv == "u") return e1 + dr * nalfa;
            else if (uv == "v") return e2 + dr * nalfa;
            else return Vector3.Zero;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // normala v bode (u,v) hornej casti potahovej plochy
        private Vector3 normalS(float u, float v, List<Sphere> S)
        {
            /*Vector2 X = new Vector2(u, v);
            for (int i = 0; i < sphere.Count; i++)
            {
                if ((X - sphere[i].Center.Xy).Length <= Delta * sphere[i].R) return new Vector3(0.0f, 0.0f, sphere[i].R);
            }*/
            return Vector3.Cross(dS("u", u, v, S), dS("v", u, v, S));
        }

//-----------------------------------------------------------------------------------------------------------------------

        // nakresli parcialne derivacie v bode (u,v) a normalovy vektor pre t mimo intervalu <0,1>, inak nakresli reper v i-tej hranicnej krivke hornej plochy 
        private void DrawNormalS(int i, float t, float s, float u, float v, List<Sphere> S)
        {
            Vector3 C0, C1, D0, D1;
            Vector2 X = new Vector2(u, v);

            Sphere S0 = S[i];
            Sphere S1;
            if (i != S.Count - 1) S1 = S[i + 1];
            else S1 = S[0];

            if (rozvetvena.IsChecked == true)
            {
                C1 = SurfacePoint(X, S);
                C0 = C1;
                C0.Z = -C0.Z;

                D0 = d0(i, s, S);
                D1 = d1(i, s, S);
            }
            else
            {
                C1 = SUp(u, v, casT, S);
                C0 = C1; //SDown(u, v, casT);
                C0.Z = -C0.Z;

                D0 = d0(i, s, casT, S);
                D1 = d1(i, s, casT, S);
            }
            
            float[] red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float[] green = { 0.0f, 1.0f, 0.0f, 1.0f };
            float[] magenta = { 0.5f, 0.0f, 0.5f, 1.0f };
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
            GL.LineWidth(2.0f);
            GL.Begin(PrimitiveType.Lines);

            if(reperC.IsChecked == true)
            {
                GL.Vertex3(C1);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C1 + normalS(u, v, S));
                else GL.Vertex3(C1 + normalSH(u, v, casT, S));
            }

            if(reperD.IsChecked == true)
            {
                GL.Vertex3(D0);
                GL.Vertex3(D0 + Lambda * (D0 - S0.Center).Normalized());
                GL.Vertex3(D1);
                GL.Vertex3(D1 + Lambda * (D1 - S1.Center).Normalized());
            }
            
            if (s >= 0 && s <= 1 && reperD.IsChecked == true)
            {
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, green);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, green);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(D0);
                if (rozvetvena.IsChecked == true) GL.Vertex3(D0 + dd0(i, s, S));
                else GL.Vertex3(D0 + dd0(i, s, casT, S));
                GL.Vertex3(D1);
                if (rozvetvena.IsChecked == true) GL.Vertex3(D1 + dd1(i, s, S));
                else GL.Vertex3(D1 + dd1(i, s, casT, S));

                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, magenta);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, magenta);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(D0);
                GL.Vertex3(D0 + f(i, 0, t, S));
                GL.Vertex3(D1);
                GL.Vertex3(D1 + f(i, 1, t, S));
            }

            if (t >= 0 && t <=1 && reperC.IsChecked == true)
            {
                Vector3 dss = ds(i, t, S);
                Vector3 dss2 = dsup(i, t, casT, S);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, green);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, green);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(C1);
                if(rozvetvena.IsChecked == true) GL.Vertex3(C1 + dss);
                else GL.Vertex3(C1 + dss2);
                dss.Z = -dss.Z;
                dss2.Z = -dss2.Z;
                GL.Vertex3(C0);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C0 + dss);
                else GL.Vertex3(C0 + dss2);

                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, magenta);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, magenta);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(C1);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C1 + e(i, 1, t, S));
                else GL.Vertex3(C1 + e(i, 1, t, casT, S));
                GL.Vertex3(C0);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C0 + e(i, 0, t, S));
                else GL.Vertex3(C0 + e(i, 0, t, casT, S));
            }
            /*else
            {
                if (rozvetvena.IsChecked == true)
                {
                    GL.Vertex3(C1);
                    GL.Vertex3(C1 + dS("u", u, v, S));

                    GL.Vertex3(C1);
                    GL.Vertex3(C1 + dS("v", u, v, S));
                }
                else
                {
                    GL.Vertex3(C1);
                    GL.Vertex3(C1 + dSUp("u", u, v, casT));

                    GL.Vertex3(C1);
                    GL.Vertex3(C1 + dSUp("v", u, v, casT));
                } 
            }*/
            GL.End();
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET DERIVACII NA k-TEJ HRANICNEJ KRIVKE HORNEJ POLOCHY

        //////////////////////////////////////////////////////////////////////////
        //                                                                      //
        //      VYPOCET DERIVACII NA k-TEJ HRANICNEJ KRIVKE HORNEJ POLOCHY      //
        //                                                                      //
        //////////////////////////////////////////////////////////////////////////

//-----------------------------------------------------------------------------------------------------------------------

        private float h(int i, int k, float t, List<Sphere> S)
        {
            float d;
            if (k < S.Count - 1)
            {
                d = (S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy)).Length;  //Vector2.Dot(S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy), S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy));
            }
            else
            {
                d = (S[k].Center.Xy - S[i].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy)).Length;  //Vector2.Dot(S[k].Center.Xy - S[i].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy), S[k].Center.Xy - S[i].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy));
            }
            return 1.0f / (float)Math.Pow(d - Delta * S[i].R, Eta);  //1.0f / (float)Math.Pow(d - Math.Pow(Delta * S[i].R, 2), n);
        }
        
//-----------------------------------------------------------------------------------------------------------------------

        private float dh(int i, int k, float t, List<Sphere> S)
        {
            Vector2 X;
            if (k < S.Count - 1)
            {
                X = S[k].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy);
                return dF("u", i, X.X, X.Y, Delta, S) * (S[k + 1].Center.X - S[k].Center.X) + dF("v", i, X.X, X.Y, Delta, S) * (S[k + 1].Center.Y - S[k].Center.Y);
            }
            else
            {
                X = S[k].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy);
                return dF("u", i, X.X, X.Y, Delta, S) * (S[0].Center.X - S[k].Center.X) + dF("v", i, X.X, X.Y, Delta, S) * (S[0].Center.Y - S[k].Center.Y);
            }
            /*float d, q;
            if (k < S.Count - 1)
            {
                d = Vector2.Dot(S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy), S[k + 1].Center.Xy - S[k].Center.Xy);
                q = (S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy)).Length;
                //Vector2.Dot(S[k + 1].Center.Xy - S[k].Center.Xy, S[k].Center.Xy - S[i].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy)) / (float)Math.Pow(d - Math.Pow(Delta * S[i].R, 2), n + 1);  //-n * (float)Math.Pow((sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[k + 1].Center.Xy - sphere[k].Center.Xy)).Length - Delta * sphere[i].R, -n - 1) * Vector2.Dot(sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[k + 1].Center.Xy - sphere[k].Center.Xy), sphere[k + 1].Center.Xy - sphere[k].Center.Xy) / (sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[k + 1].Center.Xy - sphere[k].Center.Xy)).Length;
            }
            else
            {
                d = Vector2.Dot(S[k].Center.Xy - S[i].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy), S[0].Center.Xy - S[k].Center.Xy);
                q = (S[k].Center.Xy - S[i].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy)).Length;
            }//return -n * (float)Math.Pow((sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[0].Center.Xy - sphere[k].Center.Xy)).Length - Delta * sphere[i].R, -n - 1) * Vector2.Dot(sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[0].Center.Xy - sphere[k].Center.Xy), sphere[0].Center.Xy - sphere[k].Center.Xy) / (sphere[k].Center.Xy - sphere[i].Center.Xy + t * (sphere[0].Center.Xy - sphere[k].Center.Xy)).Length;
            return -n * d / (q * (float)Math.Pow(q - Delta * S[i].R, n + 1));*/
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float g(int k, float t, List<Sphere> S)
        {
            float gSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                gSum += h(i, k, t, S);
            }
            return gSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float dg(int k, float t, List<Sphere> S)
        {
            float dgSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                dgSum += dh(i, k, t, S);
            }
            return dgSum;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private float dw(int i, int k, float t, List<Sphere> S)
        {
            /*Vector2 X;
            if (k < S.Count - 1)
            {
                X = S[k].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy);
                return dW("u", i, X.X, X.Y, Delta, S)*(S[k + 1].Center.X - S[k].Center.X)+ dW("v", i, X.X, X.Y, Delta, S) * (S[k + 1].Center.Y - S[k].Center.Y);
            }
            else
            {
               X = S[k].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy);
               return dW("u", i, X.X, X.Y, Delta, S) * (S[0].Center.X - S[k].Center.X) + dW("v", i, X.X, X.Y, Delta, S) * (S[0].Center.Y - S[k].Center.Y);
            }*/

            /*Vector2 X;
            if (k < S.Count - 1)
            {
                X = S[k].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy);
            }
            else
            {
                X = S[k].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy);
            }

            for (int j = 0; j < S.Count; j++)
            {
                if ((X - S[j].Center.Xy).Length - Delta * S[j].R <= eps) return 0; 
            }*/

            return (dh(i, k, t, S) * g(k, t, S) - h(i, k, t, S) * dg(k, t, S)) / (float)Math.Pow(g(k, t, S), 2);
        }

//-----------------------------------------------------------------------------------------------------------------------

        private Vector3 ds(int k, float t, List<Sphere> S)
        {
            /*Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);
            Vector3 nalfa = new Vector3(0.0f, 0.0f, 1.0f);
            Vector2 X;
            Vector3 dsSum;

            if (k < S.Count - 1)
            {
                dsSum = (S[k + 1].Center.X - S[k].Center.X) * e1 + (S[k + 1].Center.Y - S[k].Center.Y) * e2;
                X = S[k].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy);
            }
            else
            {
                dsSum = (S[0].Center.X - S[k].Center.X) * e1 + (S[0].Center.Y - S[k].Center.Y) * e2;
                X = S[k].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy);
            }

            for (int j = 0; j < S.Count; j++)
            {
                if ((X - S[j].Center.Xy).Length - Delta * S[j].R <= eps) return dsSum;  //if (Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(Delta * S[j].R, 2) <= eps) return dsSum;
                /*if (k == S.Count - 1)
                {
                    if (S[k].Center.Xy + t * (S[0].Center.Xy - S[k].Center.Xy) == S[j].Center.Xy) return KDelta(i, j);
                }
                else if (S[k].Center.Xy + t * (S[k + 1].Center.Xy - S[k].Center.Xy) == S[j].Center.Xy) return KDelta(i, j);*/
            /*}

            float rSum = 0.0f;

            for(int i = 0; i < S.Count; i++)
            {
                rSum += dw(i, k, t, S) * dR(i, S);
            }

            dsSum += rSum * nalfa;
            return dsSum;*/

            Vector3 X, v;
            List<Vector3> n0 = CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = HermiteCurve(t, S[k].Center, n0[1], n1[0], S[k + 1].Center); //v = S[k + 1].Center.Xy - S[k].Center.Xy;
                v = dHermiteCurve(t, S[k].Center, n0[1], n1[0], S[k + 1].Center);
            }
            else
            {
                X = HermiteCurve(t, S[k].Center, n0[1], n1[0], S[0].Center); //v = S[0].Center.Xy - S[k].Center.Xy;
                v = dHermiteCurve(t, S[k].Center, n0[1], n1[0], S[0].Center);
            }

            return dS("u", X.X, X.Y, S) * v[0] + dS("v", X.X, X.Y, S) * v[1];
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocita normalu vo vrchole (i,j) na segmente bocnej/tubularnej potahovej plochy
        private Vector3 ComputeNormal(int k, int i, int j)
        {
            Vector3 n = new Vector3();
            Vector3 v1;
            Vector3 v2;
            Vector3 v3;
            Vector3 v4;
            Vector3 n1;
            Vector3 n2;
            Vector3 n3;
            Vector3 n4;

            if (i > 0 && i < Lod1 - 1 && j > 0 && j < Lod1 - 1)
            {
                v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                n1 = Vector3.Cross(v1, v2);
                n2 = Vector3.Cross(v2, v3);
                n3 = Vector3.Cross(v3, v4);
                n4 = Vector3.Cross(v4, v1);
                n = (n1 + n2 + n3 + n4) / 4.0f;
            }
            else if(i == 0)
            {
                if(j > 0 && j < Lod1 - 1)
                {
                    v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                    v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                    v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                    n1 = Vector3.Cross(v1, v2);
                    n4 = Vector3.Cross(v4, v1);
                    n = (n1 + n4) / 2.0f;
                }
                else if (j == 0)
                {
                    v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                    v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                    n = Vector3.Cross(v1, v2);
                }
                else
                {
                    v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                    v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                    n = Vector3.Cross(v4, v1);
                }
            }
            else if (i == Lod1 - 1)
            {
                if (j > 0 && j < Lod1 - 1)
                {
                    v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                    v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                    v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                    n2 = Vector3.Cross(v2, v3);
                    n3 = Vector3.Cross(v3, v4);
                    n = (n2 + n3) / 2.0f;
                }
                else if (j == 0)
                {
                    v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                    v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                    n = Vector3.Cross(v2, v3);
                }
                else
                {
                    v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                    v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                    n = Vector3.Cross(v3, v4);
                }
            }
            else if (j == 0)
            {
                v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                v2 = Samples[k][i, j + 1] - Samples[k][i, j];
                v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                n1 = Vector3.Cross(v1, v2);
                n2 = Vector3.Cross(v2, v3);
                n = (n1 + n2) / 2.0f;
            }
            else if (j == Lod1 - 1)
            {
                v1 = Samples[k][i + 1, j] - Samples[k][i, j];
                v3 = Samples[k][i - 1, j] - Samples[k][i, j];
                v4 = Samples[k][i, j - 1] - Samples[k][i, j];
                n3 = Vector3.Cross(v3, v4);
                n4 = Vector3.Cross(v4, v1);
                n = (n3 + n4) / 2.0f;
            }
            return -n;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // spatna projekcia kliknuteho bodu do sceny
        public Vector3 UnProject(Vector3 mouse, Matrix4 projection, Matrix4 view, Size viewport)
        {
            Vector4 vec;

            vec.X = 2.0f * mouse.X / (float)viewport.Width - 1;
            vec.Y = -(2.0f * mouse.Y / (float)viewport.Height - 1);
            vec.Z = mouse.Z;
            vec.W = 1.0f;

            Matrix4 viewInv = Matrix4.Invert(view);
            Matrix4 projInv = Matrix4.Invert(projection);

            Vector4.Transform(ref vec, ref projInv, out vec);
            Vector4.Transform(ref vec, ref viewInv, out vec);

            if (vec.W > 0.000001f || vec.W < -0.000001f)
            {
                vec.X /= vec.W;
                vec.Y /= vec.W;
                vec.Z /= vec.W;
            }

            return vec.Xyz;
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET BODOV TUBULARNEJ POTAHOVEJ PLOCHY  

        ////////////////////////////////////////////////////////////
        //                                                        //
        //        VYPOCET BODOV TUBULARNEJ POTAHOVEJ PLOCHY       //
        //                                                        //
        ////////////////////////////////////////////////////////////

//-----------------------------------------------------------------------------------------------------------------------

        // kubicka Hermitova krivka interpolujuca stredy sfer Si, Si+1 a normalove vektory ni, ni+1 rovin v ktorych lezia dotykove kruznice
        private Vector3 t(int i, float u)
        {
            return H3(0, u) * Spheres[i].Center + H3(1, u) * Normals[i] + H3(2, u) * Normals[i + 1] + H3(3, u) * Spheres[i + 1].Center;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // derivacia tejto krivky
        private Vector3 dt(int i, float u)
        {
            return dH3(0, u) * Spheres[i].Center + dH3(1, u) * Normals[i] + dH3(2, u) * Normals[i + 1] + dH3(3, u) * Spheres[i + 1].Center;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // usecka medzi stredom sfery Si a Si+1
        private Vector2 C(int i, float u)
        {
            return Spheres[i].Center.Xy + u * (Spheres[i + 1].Center.Xy - Spheres[i].Center.Xy);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // "konturova" krivka ktoru budeme potom rotovat
        private Vector3 Contour(int i, float u)
        {
            List<Sphere> S = new List<Sphere>();
            S.Add(Spheres[i]);
            S.Add(Spheres[i + 1]);
            Vector2 X = C(i, u);
            /*if (TubularHomotopy.IsChecked == true && TubularHomotopy.IsEnabled == true)
            {
                SetRefPoint(S);
                Vector3 sup = SUp(X.X, X.Y, casT, S);
                return sup + (t(i, u) - new Vector3(sup.X, sup.Y, 0.0f));
            }
            else */return SurfacePoint(X, S) + (t(i, u) - new Vector3(X.X, X.Y, 0.0f));
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vysledny i-ty segment tubularnej plochy, ktory vznikne rotaciou konturovej krivky Contour(i,u) okolo osi danej t(i,u) a dt(i,u)
        private Vector3 T(int i, float u, float v)
        {
            Vector3 s = dt(i, u).Normalized();
            Vector3 k = t(i, u);
            Vector3 l = Contour(i, u) - k;
            float fi = 2.0f * (float)Math.PI * v;
            return k + l * (float)Math.Cos(fi) + Vector3.Cross(s, l) * (float)Math.Sin(fi);
            /*float fi = 2.0f * (float)Math.PI * v;
            return M(Contur(i, u), fi, new Vector3(t(i, u).X, t(i, u).Y, 0.0f), new Vector3(dt(i, u).X, dt(i, u).Y, 0.0f));*/
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocita normalove vektory ni rovin v ktorych lezia dotykove kruznice
        private void ComputeTubularNormals()
        {
            /* normal.Clear();
             Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
             normal.Add((sphere[1].Center - sphere[0].Center) / 2);
             for (int i = 1; i < sphere.Count - 1; i++)
             {
                 float d = (sphere[i + 1].Center - sphere[i - 1].Center).Length / 2.0f;
                 Vector3 t1 = sphere[i].Center - sphere[i - 1].Center;
                 Vector3 t2 = sphere[i + 1].Center - sphere[i].Center;
                 float theta = -(float)Math.Acos(Vector3.Dot(t1, t2) / (t1.Length * t2.Length)); //Vector3.CalculateAngle(t1, t2); //(float)Math.Acos(Vector3.Dot(t1, t2) / (t1.Length * t2.Length));
                 Vector3 rotC = M(sphere[i + 1].Center, theta / 2.0f, sphere[i].Center, e3);
                 normal.Add((rotC - sphere[i].Center) * d / t2.Length);
             }
             normal.Add((sphere[sphere.Count - 1].Center - sphere[sphere.Count - 2].Center) / 2.0f);*/
            if (Spheres.Count < 2) return;
            if (Spheres.Count == 2)
            {
                Normals.Add((Spheres[1].Center - Spheres[0].Center) / 2.0f);
                Normals.Add((Spheres[1].Center - Spheres[0].Center) / 2.0f);
            }
            else
            {
                Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
                Vector3 t1 = Spheres[Spheres.Count - 2].Center - Spheres[Spheres.Count - 3].Center;
                Vector3 t2 = Spheres[Spheres.Count - 1].Center - Spheres[Spheres.Count - 2].Center;
                float d = (t1 + t2).Length / 2.0f;
                float theta = (float)Math.Acos(Vector3.Dot(t1, t2) / (t1.Length * t2.Length)); //Vector3.CalculateAngle(t1, t2);
                if (det(t2.Xy, t1.Xy) <= eps) theta = -theta;
                Vector3 Rt2 = t2 * (float)Math.Cos((double)theta / 2.0f) + Vector3.Cross(e3, t2) * (float)Math.Sin((double)theta / 2.0f);  //Vector3 rotC = M(Spheres[Spheres.Count - 1].Center, theta / 2.0f, Spheres[Spheres.Count - 2].Center, e3);
                Normals[Spheres.Count - 2] = Rt2 * d / t2.Length;  //(rotC - Spheres[Spheres.Count - 2].Center) * d / t2.Length;
                Normals.Add((Spheres[Spheres.Count - 1].Center - Spheres[Spheres.Count - 2].Center) / 2.0f);
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // zobrazi normalove vektory ni
        private void DrawTubularNormals()
        {
            if (Spheres.Count > 1 && ShowTubularNormals.IsChecked == true)
            {
                for (int i = 0; i < Spheres.Count; i++)
                {
                    //float[] diffuse = { 0.5f, 0.0f, 0.5f, 1.0f };
                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, diffuse);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, diffuse);
                    GL.LineWidth(2.0f);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Color3(0.5f, 0.0f, 0.5f);
                    GL.Vertex3(Spheres[i].Center);
                    GL.Vertex3(Spheres[i].Center + Normals[i]);
                    GL.End();

                    //float[] diffuse2 = { 0.0f, 0.0f, 1.0f, 1.0f };
                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Point);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, diffuse2);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, diffuse2);
                    GL.PointSize(8.0f);
                    GL.Color3(0.0f, 0.0f, 1.0f);
                    GL.Begin(PrimitiveType.Points);
                    GL.Vertex3(Spheres[i].Center + Normals[i]);
                    GL.End();
                }
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // najde priesecnik luca, ktory ziskame inverznou transformaciou bodu kde sme klikli mysou, s normalovymi vektormi
        private Vector3 FindIntersection(List<Sphere> S, List<Vector3> normals, float r, Vector2 mouse)
        {
            float t = float.MaxValue;
            float tP = t;

            Vector3 A, B;

            int[] viewport = new int[4];
            Matrix4 modelMatrix, projMatrix;

            GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
            GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
            GL.GetInteger(GetPName.Viewport, viewport);

            A = UnProject(new Vector3(mouse.X, mouse.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
            B = UnProject(new Vector3(mouse.X, mouse.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

            Vector3 v = (B - A).Normalized();

            float a = v.Length * v.Length;

            for (int i = 0; i < S.Count; i++)
            {
                Vector3 P = S[i].Center + normals[i];
                float b = 2 * Vector3.Dot(v, A - P);
                float c = Vector3.Dot(A - P, A - P) - r * r;
                float D = b * b - 4 * a * c;

                if (D > 0) tP = (float)Math.Min((-b + Math.Sqrt(D)) / (2 * a), (-b - Math.Sqrt(D)) / (2 * a));
                else if (D == 0) tP = -b / (2 * a);

                if (tP < t)
                {
                    t = tP;
                    HitNormal = i;
                }
            }
            if (t == float.MaxValue) return Vector3.Zero;
            else return A + t * v;
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET POTAHOVEJ PLOCHY POMOCOU HOMOTOPIE
                
        //////////////////////////////////////////////////////////////
        //                                                          //
        //        VYPOCET POTAHOVEJ PLOCHY POMOCOU HOMOTOPIE        //
        //                                                          //
        //////////////////////////////////////////////////////////////

        // mean value coordinates
        private float gama(int i, float u, float v)
        {
            Vector2 X = new Vector2(u, v);
            float eps = 0.00000009f;
            for (int j = 0; j < Spheres.Count; j++)
            {
                Vector2 C0 = Spheres[j].Center.Xy;
                Vector2 C1;
                float d, t = -1;
                if (j < Spheres.Count - 1) C1 = Spheres[j + 1].Center.Xy; // d = det(Spheres[i + 1].Center.Xy - Spheres[i].Center.Xy, X - Spheres[i].Center.Xy);
                else C1 = Spheres[0].Center.Xy; //d = det(Spheres[0].Center.Xy - Spheres[i].Center.Xy, X - Spheres[i].Center.Xy);

                d = det(C1 - C0, X - C0);

                if (d <= eps && d >= -eps)
                {
                    if(C0.X != C1.X)
                    {
                        t = (u - C0.X) / (C1.X - C0.X);
                    }
                    else if (C0.Y != C1.Y)
                    {
                        t = (v - C0.Y) / (C1.Y - C0.Y);
                    }
                    if (t >= 0 && t <= 1) return (1 - t) * KDelta(i, j) + t * KDelta(i, j + 1);
                }
            }

            float Sum = 0.0f;
            for (int j = 0; j < Spheres.Count; j++)
            {
                Sum += (TanAlfaPol(j - 1, u, v) + TanAlfaPol(j, u, v)) / (Spheres[j].Center.Xy - X).Length;
            }

            return (TanAlfaPol(i - 1, u, v) + TanAlfaPol(i, u, v)) / ((Spheres[i].Center.Xy - X).Length * Sum);
        }

        private float TanAlfaPol(int i, float u, float v)
        {
            Vector2 X = new Vector2(u, v);
            Vector2 v0; 
            Vector2 v1;
            if (i < 0) v0 = Spheres[Spheres.Count - 1].Center.Xy - X;
            else v0 = Spheres[i].Center.Xy - X;

            if (i < Spheres.Count - 1) v1 = Spheres[i + 1].Center.Xy - X;
            else v1 = Spheres[0].Center.Xy - X;

            return det(v0, v1) / (v0.Length * v1.Length + Vector2.Dot(v0, v1));
        }

        private float det(Vector2 X, Vector2 Y)
        {
            return X.X * Y.Y - X.Y * Y.X;
        }
        
        // nastavenie referencneho bodu
        private void SetRefPoint(List<Sphere> S)
        {
            float rMax = 0.0f;
            int k = -1;

            for (int i = 0; i < S.Count; i++)
            {
                if (rMax < S[i].R)
                {
                    rMax = S[i].R;
                    k = i;
                }
            }
            if (k != -1) Pref = new Vector3(S[k].Center.X, S[k].Center.Y, rMax);
            rRef = rMax;
        }

        private Vector3 P(Vector2 X, Vector3 pRef)
        {
            /*Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);*/

            return new Vector3(X.X, X.Y, pRef.Z); //pRef + (X.X - pRef.X) * e1 + (X.Y - pRef.Y) * e2;
        }

        // vypocty hornej a dolnej plochy
        private Vector3 S(int i, float d, Vector3 X, List<Sphere> S)
        {
            Vector4 X2 = new Vector4(X, 1);
            Vector4 row0 = new Vector4(d, 0.0f, 0.0f, S[i].Center.X * (1.0f - d));
            Vector4 row1 = new Vector4(0.0f, d, 0.0f, S[i].Center.Y * (1.0f - d));
            Vector4 row2 = new Vector4(0.0f, 0.0f, d, 0.0f);
            Vector4 row3 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            Matrix4 Mat = new Matrix4(row0, row1, row2, row3);
            X2 = Mat * X2;
            return X2.Xyz;   
        }

        private Matrix4 M(int i, float t, List<Sphere> S)
        {
            float dit = 1.0f + t * (S[i].R / rRef - 1.0f);
            

            Vector4 row0 = new Vector4(dit, 0.0f, 0.0f, S[i].Center.X * (1.0f - dit));
            Vector4 row1 = new Vector4(0.0f, dit, 0.0f, S[i].Center.Y * (1.0f - dit));
            Vector4 row2 = new Vector4(0.0f, 0.0f, dit, 0.0f);
            Vector4 row3 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            return new Matrix4(row0, row1, row2, row3);
        }

        private Matrix4 M(float u, float v, float t, List<Sphere> S)
        {
            float dt = 0.0f;
            float ut = 0.0f, vt = 0.0f, dit;
            
            Vector2 X = new Vector2(u, v);

            Vector4 row0 = Vector4.Zero;
            Vector4 row1 = Vector4.Zero;
            Vector4 row2 = Vector4.Zero;
            Vector4 row3 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            if(ModifShep.IsChecked == true)
            {
                for (int i = 0; i < S.Count; i++)
                {
                    dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                    dt += w(i, X, S, 0.0f) * dit;
                    ut += w(i, X, S, 0.0f) * S[i].Center.X * (1.0f - dit);
                    vt += w(i, X, S, 0.0f) * S[i].Center.Y * (1.0f - dit);
                }
            }
            if(MeanValue.IsChecked == true)
            {
                for (int i = 0; i < S.Count; i++)
                {
                    dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                    dt += gama(i, u, v) * dit;
                    ut += gama(i, u, v) * S[i].Center.X * (1.0f - dit);
                    vt += gama(i, u, v) * S[i].Center.Y * (1.0f - dit);
                }
            }
            

            row0[0] = dt;
            row0[3] = ut;

            row1[1] = dt;
            row1[3] = vt;

            row2[2] = dt;

            return new Matrix4(row0, row1, row2, row3);
        }

        private Vector3 S(int i, Vector3 X, float t, List<Sphere> S)
        {
            return (M(i, t, S) * new Vector4(X, 1.0f)).Xyz; //(1 - t) * S(i, 1, X) + t * S(i, Spheres[i].R / rRef, X);
        }

        private Vector3 SUp(float u, float v, float t, List<Sphere> S)
        {
            return (M(u, v, t, S) * new Vector4(u, v, Pref.Z, 1.0f)).Xyz; //(M(u, v, t, S) * new Vector4(P(new Vector2(u, v), Pref), 1.0f)).Xyz;
        }

        private Vector3 SDown(float u, float v, float t, List<Sphere> S)
        {
            Vector3 Suvt = SUp(u, v, t, S);
            Suvt.Z = -Suvt.Z;
            return Suvt;
        }
        
        // parcialne derivacie hornej casti
        private Vector3 dSUp(string uv, float u, float v, float t, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            Vector3 ds = Vector3.Zero;
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);

            for (int i = 0; i < S.Count; i++)
            {
                if (Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) <= eps && uv == "u") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e1;
                if (Vector2.Dot(X - S[i].Center.Xy, X - S[i].Center.Xy) <= eps && uv == "v") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e2;
            }

            float dit, dwi;
            float SumWD = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                dwi = dW(uv, i, u, v, 0.0f, S);
                ds[0] += dwi * (u * dit + S[i].Center.X * (1.0f - dit));
                ds[1] += dwi * (v * dit + S[i].Center.Y * (1.0f - dit));
                ds[2] += dwi * rRef * dit;
                SumWD += w(i, X, S, 0.0f) * dit;
            }

            if (uv == "u") return ds + SumWD * e1;
            else if (uv == "v") return ds + SumWD * e2;
            else return Vector3.Zero;
        }

        // normalovy vektor hornej casti
        private Vector3 normalSH(float u, float v, float t, List<Sphere> S)
        {
            return Vector3.Cross(dSUp("u", u, v, t, S), dSUp("v", u, v, t, S));
        }

        // vypocet derivacii k-tej hranicnej krivky
        private Vector3 dsup(int k, float u, float t, List<Sphere> S)
        {
            Vector3 X, s;
            List<Vector3> n0 = CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = HermiteCurve(u, S[k].Center, n0[1], n1[0], S[k + 1].Center);
                s = dHermiteCurve(u, S[k].Center, n0[1], n1[0], S[k + 1].Center);
            }
            else
            {
                X = HermiteCurve(u, S[k].Center, n0[1], n1[0], S[0].Center);
                s = dHermiteCurve(u, S[k].Center, n0[1], n1[0], S[0].Center);
            }

            /*if (k < Spheres.Count - 1) s = Spheres[k + 1].Center - Spheres[k].Center;
            else s = Spheres[0].Center - Spheres[k].Center;*/

            //X = Spheres[k].Center + u * s;
            return dSUp("u", X.X, X.Y, t, S) * s[0] + dSUp("v", X.X, X.Y, t, S) * s[1];
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET COONSOVEJ BIKUBICKY STMELOVANEJ ZAPLATY SEGMENTU BOCNEJ PLOCHY POMOCOU HOMOTOPIE

        ///////////////////////////////////////////////////////////////////////////////////////////////////////
        //                                                                                                   //
        //      VYPOCET COONSOVEJ BIKUBICKY STMELOVANEJ ZAPLATY SEGMENTU BOCNEJ PLOCHY POMOCOU HOMOTOPIE     //
        //                                                                                                   //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////

        // hranicna krivka c0 Coonsovej zaplaty v case t
        private Vector3 c0(int i, float u, float t, List<Sphere> S)
        {
            Vector3 X = c1(i, u, t, S);
            X.Z = -X.Z;
            return X;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka c1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 c1(int i, float u, float t, List<Sphere> S)
        {
            Vector3 X;
            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[i + 1].Center); //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
            else X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[0].Center); //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
            return SUp(X.X, X.Y, t, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d0 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 d0(int i, float v, float t, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].Center - S[i].Center;
            else ti3 = S[0].Center - S[i].Center;
            n = ti3.Xy;

            if (i > 0) s = S[i].Center.Xy - S[i - 1].Center.Xy;
            else s = S[i].Center.Xy - S[S.Count - 1].Center.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            float dit = 1.0f + (1.0f - t) * (rRef / S[i].R - 1.0f);
            return S[i].Center + S[i].R * dit * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - S[i].R * dit * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;
        }

        // derivacia hranicnej krivky d0 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 dd0(int i, float v, float t, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].Center - S[i].Center;
            else ti3 = S[0].Center - S[i].Center;
            n = ti3.Xy;

            if (i > 0) s = S[i].Center.Xy - S[i - 1].Center.Xy;
            else s = S[i].Center.Xy - S[S.Count - 1].Center.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            float dit = 1.0f + (1.0f - t) * (rRef / S[i].R - 1.0f);
            return -S[i].R * dit * (float)(Math.PI * Math.Sin(Math.PI * ((double)v + 1))) * e3 - S[i].R * dit * (float)(Math.PI * Math.Cos(Math.PI * ((double)v + 1))) * ti2;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 d1(int i, float v, float t, List<Sphere> S)
        {
            //Vector3 X;
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Sphere C1;
            Vector2 s, n;
            Vector3 ti3;

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Center, S[0].R);
            s = C1.Center.Xy - S[i].Center.Xy;

            if (i < S.Count - 2) ti3 = S[i + 2].Center - S[i + 1].Center;
            else if (i == S.Count - 2) ti3 = S[0].Center - S[i + 1].Center;
            else ti3 = S[1].Center - S[0].Center;

            n = ti3.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }
            else ti3 = new Vector3(s.X, s.Y, 0.0f);

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            float dit = 1.0f + (1.0f - t) * (rRef / C1.R - 1.0f);
            return C1.Center + C1.R * dit * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - C1.R * dit * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;
        }

        // derivacia hranicnej krivky d1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 dd1(int i, float v, float t, List<Sphere> S)
        {
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Sphere C1;
            Vector2 s, n;
            Vector3 ti3;

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Center, S[0].R);
            s = C1.Center.Xy - S[i].Center.Xy;

            if (i < S.Count - 2) ti3 = S[i + 2].Center - S[i + 1].Center;
            else if (i == S.Count - 2) ti3 = S[0].Center - S[i + 1].Center;
            else ti3 = S[1].Center - S[0].Center;

            n = ti3.Xy;

            if (det(s, n) < -eps)
            {
                n = s + n;
                ti3 = new Vector3(n.X, n.Y, 0.0f);
            }
            else ti3 = new Vector3(s.X, s.Y, 0.0f);

            Vector3 ti2 = Vector3.Cross(ti3, e3).Normalized();

            float dit = 1.0f + (1.0f - t) * (rRef / C1.R - 1.0f);
            return -C1.R * dit * (float)(Math.PI * Math.Sin(Math.PI * ((double)v + 1))) * e3 - C1.R * dit * (float)(Math.PI * Math.Cos(Math.PI * ((double)v + 1))) * ti2;
        }

//-----------------------------------------------------------------------------------------------------------------------

        // j-ta (j=0,1) funkcia derivacii ej na i-tom segmente bocnej potahovej plochy v case t
        private Vector3 e(int i, int j, float u, float t, List<Sphere> S)
        {
            Vector3 X;  //v;
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);

            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[i + 1].Center);
            else X = HermiteCurve(u, S[i].Center, n0[1], n1[0], S[0].Center);

            /*if (i < Spheres.Count - 1)
            {
                //v = Vector3.Cross(Spheres[i + 1].Center - Spheres[i].Center, e3);
                v = Spheres[i + 1].Center - Spheres[i].Center;
                //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
                X = Spheres[i].Center + u * v;
            }
            else
            {
                //v = Vector3.Cross(Spheres[0].Center - Spheres[i].Center, e3);
                v = Spheres[0].Center - Spheres[i].Center;
                //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
                X = Spheres[i].Center + u * v;
            }*/

            //float tau = Lambda * v.Length;
            Vector3 nS;
            Vector3 dcij;

            nS = normalSH(X.X, X.Y, t, S);
            dcij = dsup(i, u, t, S);

            if (j == 0)
            {
                nS.Z = -nS.Z;
                dcij.Z = -dcij.Z;
            }

            return Tau * Vector3.Cross(nS, dcij).Normalized();
        }

//-----------------------------------------------------------------------------------------------------------------------

        // prva tvoriaca zaplata v case t
        private Vector3 Sc(int i, float u, float v, float t, List<Sphere> S)
        {
            return H3(0, v) * c0(i, u, t, S) + H3(1, v) * e(i, 0, u, t, S) + H3(2, v) * e(i, 1, u, t, S) + H3(3, v) * c1(i, u, t, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // druha tvoriaca zaplata v case t
        private Vector3 Sd(int i, float u, float v, float t, List<Sphere> S)
        {
            return H3(0, u) * d0(i, v, t, S) + H3(1, u) * f(i, 0, v, S) + H3(2, u) * f(i, 1, v, S) + H3(3, u) * d1(i, v, t, S);
        }

//-----------------------------------------------------------------------------------------------------------------------

        // korekcna zaplata v case t
        private Vector3 Scd(int i, float u, float v, float t, List<Sphere> S)
        {
            Vector4 Hu = new Vector4(H3(0, u), H3(1, u), H3(2, u), H3(3, u));
            Vector4 Hv = new Vector4(H3(0, v), H3(1, v), H3(2, v), H3(3, v));
            /*Vector3 t00 = Vector3.Zero;
            Vector3 t10 = Vector3.Zero;
            Vector3 t01 = Vector3.Zero;
            Vector3 t11 = Vector3.Zero;*/

            return Hv[0] * (Hu[0] * c0(i, 0, t, S) + Hu[1] * f(i, 0, 0, S) + Hu[2] * f(i, 1, 0, S) + Hu[3] * c0(i, 1, t, S)) +
                   Hv[1] * (Hu[0] * e(i, 0, 0, t, S) /*+ Hu[1] * t00 + Hu[2] * t01*/ + Hu[3] * e(i, 0, 1, t, S)) +
                   Hv[2] * (Hu[0] * e(i, 1, 0, t, S) /*+ Hu[1] * t10 + Hu[2] * t11*/ + Hu[3] * e(i, 1, 1, t, S)) +
                   Hv[3] * (Hu[0] * c1(i, 0, t, S) + Hu[1] * f(i, 0, 1, S) + Hu[2] * f(i, 1, 1, S) + Hu[3] * c1(i, 1, t, S));
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocita bod na Coonsovej zaplate na i-tom segmente v case t
        private Vector3 CoonsPatchPoint(int i, float u, float v, float t, List<Sphere> S)
        {
            return Sc(i, u, v, t, S) + Sd(i, u, v, t, S) - Scd(i, u, v, t, S);
        }
        #endregion

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYKRESLENIE PROSTREDIA

        /////////////////////////////////////////////////////
        //                                                 //
        //            VYKRESLOVANIE PROSTREDIA             //
        //                                                 //
        /////////////////////////////////////////////////////

        // vykreslovanie suradnicovych osi
        private void DrawAxes()
        {
            float m = 10;
            GL.LineWidth(2.0f);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(1.0f, 0.0f, 0.0f);
            GL.Vertex3(-m, 0.0f, 0.0f);
            GL.Color3(1.0f, 0.0f, 0.0f);
            GL.Vertex3(m, 0.0f, 0.0f);

            GL.Color3(0.0f, 1.0f, 0.0f);
            GL.Vertex3(0.0f, -m, 0.0f);
            GL.Color3(0.0f, 1.0f, 0.0f);
            GL.Vertex3(0.0f, m, 0.0f);

            GL.Color3(0.0f, 0.0f, 1.0f);
            GL.Vertex3(0.0f, 0.0f, -m);
            GL.Color3(0.0f, 0.0f, 1.0f);
            GL.Vertex3(0.0f, 0.0f, m);
            GL.End();
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vykreslenie siete roviny z = 0
        private void DrawGrid()
        {
            float gray = 0.5f;
            float alpha = 0.25f;

            float m = 10.0f;
            int n = 200;
            float h = 2 * m / n;

            GL.LineWidth(1.0f);


            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    GL.Begin(PrimitiveType.LineLoop);
                    GL.Color4(gray, gray, gray, alpha);
                    GL.Vertex3(-m + j * h, -m + i * h, 0.0f);
                    GL.Vertex3(-m + (j + 1) * h, -m + i * h, 0.0f);
                    GL.Vertex3(-m + (j + 1) * h, -m + (i + 1) * h, 0.0f);
                    GL.Vertex3(-m + j * h, -m + (i + 1) * h, 0.0f);
                    GL.End();
                }
            }
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region VYPOCET A VYKRESLENIE POTAHOVEJ PLOCHY

        ///////////////////////////////////////////////////////////////
        //                                                           //
        //         VYPOCET A VYKRESLOVANIE POTAHOVEJ PLOCHY          //
        //                                                           //
        ///////////////////////////////////////////////////////////////

        // vypocet bodov tubularnej potahovej plochy
        private void ComputeTubularSurface(int lod)
        {
           // ComputeTubularNormals();

            for (int i = 0; i < Spheres.Count - 1; i++)
            {
                for (int j = 0; j < lod; j++)
                {
                    float u = (float)j / (lod - 1);
                    for (int k = 0; k < lod; k++)
                    {
                        float v = (float)k / (lod - 1);
                        Samples[i][j, k] = T(i, u, v);
                    }
                }
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vykreslenie tubularnej potahovej plochy
        private void DrawTubularSurface(int lod)
        {
            float[] Diffuse, Ambient, Specular, red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float Shininess = 0.7f;

            if (ColorOne)
            {
                Diffuse = new float[] { 0.0f, 1.0f, 0.0f, 1.0f };
                Ambient = new float[] { 0.0f, 1.0f, 0.0f, 1.0f };
                Specular = new float[] { 0.05f, 0.05f, 0.05f, 0.5f };
                Shininess = 0.1f;
            }
            else
            {
                Diffuse = new float[] { 1.0f, 1.0f, 0.0f, 1.0f };
                Ambient = new float[] { 1.0f, 0.55f, 0.0f, 1.0f };
                Specular = new float[] { 0.6f, 0.6f, 0.3f, 1.0f };
            }

            // prechadzame jednotlivymi segmentami
            for (int i = 0; i < Spheres.Count - 1; i++)
            {
                // vykreslenie segmentu tubularnej plochy
                for (int j = 0; j < lod - 1; j++)
                {
                    for (int k = 0; k < lod - 1; k++)
                    {
                        if (checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);


                            // vypocet normal vo vrcholoch
                            Vector3 n1 = ComputeNormal(i, j, k).Normalized();
                            Vector3 n2 = ComputeNormal(i, j + 1, k).Normalized();
                            Vector3 n3 = ComputeNormal(i, j + 1, k + 1).Normalized();
                            Vector3 n4 = ComputeNormal(i, j, k + 1).Normalized();

                            GL.Begin(PrimitiveType.Quads);
                            GL.Normal3(n1);
                            GL.Vertex3(Samples[i][j, k]);
                            GL.Normal3(n2);
                            GL.Vertex3(Samples[i][j + 1, k]);
                            GL.Normal3(n3);
                            GL.Vertex3(Samples[i][j + 1, k + 1]);
                            GL.Normal3(n4);
                            GL.Vertex3(Samples[i][j, k + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(Samples[i][j, k]);
                            GL.Vertex3(Samples[i][j + 1, k]);
                            GL.Vertex3(Samples[i][j + 1, k + 1]);
                            GL.Vertex3(Samples[i][j, k + 1]);
                            GL.End();
                        }
                    }
                }
            }
        }

        private void DrawTubularIsoLines(int n, int lod)
        {
            Vector3[,] Pts = new Vector3[n,lod];
            float[] blue = { 0.0f, 0.0f, 1.0f, 1.0f };
            float[] magenta = { 1.0f, 0.0f, 1.0f, 1.0f };

            if (UCurves.IsChecked == true)
            {
                for (int i = 0; i < Spheres.Count - 1; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        float v = (float)j / n;
                        for (int k = 0; k < lod; k++)
                        {
                            float u = (float)k / (lod - 1);
                            Pts[j, k] = T(i, u, v);
                        }
                    }

                    // vykreslenie segmentu tubularnej plochy
                    for (int j = 0; j < n; j++)
                    {
                        for (int k = 0; k < lod - 1; k++)
                        {
                            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, blue);
                            //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, blue);
                            GL.LineWidth(2.0f);
                            GL.Begin(PrimitiveType.Lines);
                            GL.Color3(0.0f, 0.0f, 1.0f);
                            GL.Vertex3(Pts[j, k]);
                            GL.Vertex3(Pts[j, k + 1]);
                            GL.End();
                        }
                    }
                }
            }
            if (VCurves.IsChecked == true)
            {
                for (int i = 0; i < Spheres.Count - 1; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        float u = (float)j / n;
                        for (int k = 0; k < lod; k++)
                        {
                            float v = (float)k / (lod - 1);
                            Pts[j, k] = T(i, u, v);
                        }
                    }

                    // vykreslenie segmentu tubularnej plochy
                    for (int j = 1; j < n-1; j++)
                    {
                        //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, blue);
                        //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, blue);
                        GL.LineWidth(2.0f);
                        GL.Begin(PrimitiveType.LineLoop);
                        GL.Color3(0.0f, 0.0f, 1.0f);
                        for (int k = 0; k < lod; k++)
                        {
                            GL.Vertex3(Pts[j, k]);
                        }
                        GL.End();
                    }
                }
            }

            // vykreslenie dotykovych kruznic
            for (int i = 0; i < Spheres.Count - 1; i++)
            {
                for (int k = 0; k < Lod1 - 1; k++)
                {
                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, magenta);
                    //GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, magenta);
                    GL.LineWidth(4.0f);
                    GL.Begin(PrimitiveType.Lines);
                    GL.Color3(0.75f, 0.0f, 0.75f);
                    GL.Vertex3(Samples[i][0, k]);
                    GL.Vertex3(Samples[i][0, k + 1]);
                    if (i == Spheres.Count - 2)
                    {
                        GL.Vertex3(Samples[i][Lod1 - 1, k]);
                        GL.Vertex3(Samples[i][Lod1 - 1, k + 1]);
                    }
                    GL.End();
                }
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocet bodov hornej a dolnej casti potahovej plochy
        private void ComputeUpDownSurface(List<Sphere> S, Vector3 T, int lod)
        {
            Stopwatch stopWatch1 = new Stopwatch();
            Stopwatch stopWatch2 = new Stopwatch();
            int n;
            float t0, t1, t2;
            Vector3 t;

            if (Homotopy)
            {
                stopWatch1.Start();
                for (int i = 0; i < S.Count; i++)
                {
                    Vector3 C0 = S[i].Center;
                    Vector3 C1;
                    if (i != S.Count - 1) C1 = S[i + 1].Center;
                    else C1 = S[0].Center;
                    Vector3 X;
                    List<Vector3> n0 = CheckKonvexAt(i, 0, S);
                    List<Vector3> n1 = CheckKonvexAt(i, 1, S);

                    if (det(n0[1].Xy, n1[0].Xy) < eps && det(n0[1].Xy, n1[0].Xy) > -eps)
                    {
                        // vypocet vo vzorkovacich bodoch
                        for (int l = lod; l >= 0; l--)
                        {
                            for (int m = lod - l; m >= 0; m--)
                            {
                                n = lod - (l + m);
                                t0 = (float)l / lod;
                                t1 = (float)m / lod;
                                t2 = (float)n / lod;
                                t = C0 * t0 + C1 * t1 + T * t2;
                                SamplesUpDown[i][l, m, n] = SUp(t.X, t.Y, casT, S);
                            }
                        }
                    }
                    else
                    {
                        // vypocet vo vzorkovacich bodoch
                        for (int l = lod; l >= 0; l--)
                        {
                            for (int m = lod - l; m >= 0; m--)
                            {
                                n = lod - (l + m);
                                t0 = (float)l / lod;
                                t1 = (float)m / lod;
                                t2 = (float)n / lod;
                                Vector3 c0 = C0 + (1.0f - t0) * (T - C0);
                                Vector3 h = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                                X = c0 * t2 / (t1 + t2) + h * t1 / (t1 + t2);
                                SamplesUpDown[i][l, m, n] = SUp(X.X, X.Y, casT, S);
                            }
                        }

                        SamplesUpDown[i][lod, 0, 0] = SUp(C0.X, C0.Y, casT, S);

                        t0 = (float)(lod - 1.0f) / lod;
                        t1 = 1.0f / lod;
                        t = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                        SamplesUpDown[i][lod - 1, 1, 0] = SUp(t.X, t.Y, casT, S);

                        t0 = (float)(lod - 1.0f) / lod;
                        t2 = 1.0f / lod;
                        t = C0 + (1.0f - t0) * (T - C0);
                        SamplesUpDown[i][lod - 1, 0, 1] = SUp(t.X, t.Y, casT, S);
                    }
                    
                }
                stopWatch1.Stop();
                casHom += stopWatch1.ElapsedMilliseconds;
            }
            else
            {
                stopWatch2.Start();
                for (int i = 0; i < S.Count; i++)
                {
                    Vector3 C0 = S[i].Center;
                    Vector3 C1;
                    if (i != S.Count - 1) C1 = S[i + 1].Center;
                    else C1 = S[0].Center;
                    Vector3 X;
                    List<Vector3> n0 = CheckKonvexAt(i, 0, S);
                    List<Vector3> n1 = CheckKonvexAt(i, 1, S);

                    if (det(n0[1].Xy, n1[0].Xy) < eps && det(n0[1].Xy, n1[0].Xy) > -eps)
                    {
                        // vypocet vo vzorkovacich bodoch
                        for (int l = lod; l >= 0; l--)
                        {
                            for (int m = lod - l; m >= 0; m--)
                            {
                                n = lod - (l + m);
                                t0 = (float)l / lod;
                                t1 = (float)m / lod;
                                t2 = (float)n / lod;
                                t = C0 * t0 + C1 * t1 + T * t2;
                                SamplesUpDown[i][l, m, n] = SurfacePoint(t.Xy, S);
                            }
                        }
                    }
                    else
                    {
                        // vypocet vo vzorkovacich bodoch
                        for (int l = lod; l >= 0; l--)
                        {
                            for (int m = lod - l; m >= 0; m--)
                            {
                                n = lod - (l + m);
                                t0 = (float)l / lod;
                                t1 = (float)m / lod;
                                t2 = (float)n / lod;
                                Vector3 c0 = C0 + (1.0f - t0) * (T - C0);
                                Vector3 h = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                                X =  c0 * t2 / (t1 + t2) + h * t1 / (t1 + t2);
                                SamplesUpDown[i][l, m, n] = SurfacePoint(X.Xy, S);
                            }
                        }

                        SamplesUpDown[i][lod, 0, 0] = SurfacePoint(C0.Xy, S);

                        t0 = (float)(lod - 1.0f) / lod;
                        t1 = 1.0f / lod;
                        t = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                        SamplesUpDown[i][lod - 1, 1, 0] = SurfacePoint(t.Xy, S);

                        t0 = (float)(lod - 1.0f) / lod;
                        t2 = 1.0f / lod;
                        t = C0 + (1.0f - t0) * (T - C0);
                        SamplesUpDown[i][lod - 1, 0, 1] = SurfacePoint(t.Xy, S);
                    }
                        
                }
                stopWatch2.Stop();
                casRozv = +stopWatch2.ElapsedMilliseconds;
            }
    }

//-----------------------------------------------------------------------------------------------------------------------

        // vykreslenie hornej a dolnej casti potahovej plochy
        private void DrawUpDownSurface(List<Sphere> S, int lod)
        {
            float[] red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float[] Diffuse, Ambient, Specular = { 0.5f, 0.5f, 0.5f, 1.0f };
            float Shininess = 0.5f;
            if (ColorOne)
            {
                Diffuse = new float[] { 0.0f, 1.0f, 0.0f, 1.0f };
                Ambient = new float[] { 0.0f, 1.0f, 0.0f, 1.0f };
                Specular = new float[] { 0.05f, 0.05f, 0.05f, 0.5f };
                Shininess = 0.1f;
            }
            else
            {
                Diffuse = new float[] { 0.0f, 0.5f, 0.0f, 1.0f };
                Ambient = new float[] { 0.5f, 0.5f, 0.0f, 1.0f };
            }

            // prechadzame jednotlivymi trojuholnikmi danymi S[i].Center, S[i+1].Center a T (taziskom vsetkych sfer)
            for (int i = 0; i < S.Count; i++)
            {
                /*if (checkBox4.IsChecked == true)
                {
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, c);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, c);

                    Vector3 normal = Vector3.Cross(SamplesUpDown[i][lod - 1, 1, 0] - SamplesUpDown[i][lod, 0, 0], SamplesUpDown[i][lod - 1, 0, 1] - SamplesUpDown[i][lod - 1, 1, 0]).Normalized();

                    GL.Begin(PrimitiveType.Triangles);
                    GL.Normal3(normal);
                    GL.Vertex3(SamplesUpDown[i][lod, 0, 0]);
                    GL.Vertex3(SamplesUpDown[i][lod - 1, 1, 0]);
                    GL.Vertex3(SamplesUpDown[i][lod - 1, 0, 1]);
                    GL.End();

                    SamplesUpDown[i][lod, 0, 0].Z = -SamplesUpDown[i][lod, 0, 0].Z;
                    SamplesUpDown[i][lod - 1, 1, 0].Z = -SamplesUpDown[i][lod - 1, 1, 0].Z;
                    SamplesUpDown[i][lod - 1, 0, 1].Z = -SamplesUpDown[i][lod - 1, 0, 1].Z;

                    normal.Z = -normal.Z; //Vector3.Cross(SamplesUpDown[i][lod - 1, 0, 1] - SamplesUpDown[i][lod - 1, 1, 0], SamplesUpDown[i][lod - 1, 1, 0] - SamplesUpDown[i][lod, 0, 0]).Normalized();

                    GL.Begin(PrimitiveType.Triangles);
                    GL.Normal3(normal);
                    GL.Vertex3(SamplesUpDown[i][lod, 0, 0]);
                    GL.Vertex3(SamplesUpDown[i][lod - 1, 1, 0]);
                    GL.Vertex3(SamplesUpDown[i][lod - 1, 0, 1]);
                    GL.End();

                    SamplesUpDown[i][lod, 0, 0].Z = -SamplesUpDown[i][lod, 0, 0].Z;
                    SamplesUpDown[i][lod - 1, 1, 0].Z = -SamplesUpDown[i][lod - 1, 1, 0].Z;
                    SamplesUpDown[i][lod - 1, 0, 1].Z = -SamplesUpDown[i][lod - 1, 0, 1].Z;
                }*/


                // vykreslenie hornej/dolnej casti plochy
                for (int l = lod; l > 0; l--)
                {
                    for (int m = lod - l; m >= 0; m--)
                    {
                        int n = lod - (l + m);
                        if (checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                            Vector3 normal = Vector3.Cross(SamplesUpDown[i][l - 1, m + 1, n] - SamplesUpDown[i][l, m, n], SamplesUpDown[i][l - 1, m, n + 1] - SamplesUpDown[i][l - 1, m + 1, n]).Normalized();

                            GL.Begin(PrimitiveType.Triangles);
                            GL.Normal3(normal);
                            GL.Vertex3(SamplesUpDown[i][l, m, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(SamplesUpDown[i][l, m, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        // transformujeme vzorkovacie vrcholy na vykreslenie spodnej casti plochy
                        SamplesUpDown[i][l, m, n].Z = -SamplesUpDown[i][l, m, n].Z;
                        SamplesUpDown[i][l - 1, m + 1, n].Z = -SamplesUpDown[i][l - 1, m + 1, n].Z;
                        SamplesUpDown[i][l - 1, m, n + 1].Z = -SamplesUpDown[i][l - 1, m, n + 1].Z;

                        if (checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                            Vector3 normal = Vector3.Cross(SamplesUpDown[i][l - 1, m, n + 1] - SamplesUpDown[i][l - 1, m + 1, n], SamplesUpDown[i][l - 1, m + 1, n] - SamplesUpDown[i][l, m, n]).Normalized();

                            GL.Begin(PrimitiveType.Triangles);
                            GL.Normal3(normal);
                            GL.Vertex3(SamplesUpDown[i][l, m, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(SamplesUpDown[i][l, m, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        // transformujeme naspat 
                        SamplesUpDown[i][l, m, n].Z = -SamplesUpDown[i][l, m, n].Z;
                        SamplesUpDown[i][l - 1, m + 1, n].Z = -SamplesUpDown[i][l - 1, m + 1, n].Z;
                        SamplesUpDown[i][l - 1, m, n + 1].Z = -SamplesUpDown[i][l - 1, m, n + 1].Z;

                        if (m > 0)
                        {
                            if (checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesUpDown[i][l - 1, m, n + 1] - SamplesUpDown[i][l, m - 1, n + 1], SamplesUpDown[i][l, m - 1, n + 1] - SamplesUpDown[i][l, m, n]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesUpDown[i][l, m, n]);
                                GL.Vertex3(SamplesUpDown[i][l, m - 1, n + 1]);
                                GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                                GL.End();
                            }

                            // znova transformujeme vzorkovacie vrcholy na vykreslenie spodnej casti plochy
                            SamplesUpDown[i][l, m, n].Z = -SamplesUpDown[i][l, m, n].Z;
                            SamplesUpDown[i][l, m - 1, n + 1].Z = -SamplesUpDown[i][l, m - 1, n + 1].Z;
                            SamplesUpDown[i][l - 1, m, n + 1].Z = -SamplesUpDown[i][l - 1, m, n + 1].Z;

                            if (checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesUpDown[i][l, m - 1, n + 1] - SamplesUpDown[i][l, m, n], SamplesUpDown[i][l - 1, m, n + 1] - SamplesUpDown[i][l, m - 1, n + 1]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesUpDown[i][l, m, n]);
                                GL.Vertex3(SamplesUpDown[i][l, m - 1, n + 1]);
                                GL.Vertex3(SamplesUpDown[i][l - 1, m, n + 1]);
                                GL.End();
                            }

                            // spatne transformujeme
                            SamplesUpDown[i][l, m, n].Z = -SamplesUpDown[i][l, m, n].Z;
                            SamplesUpDown[i][l, m - 1, n + 1].Z = -SamplesUpDown[i][l, m - 1, n + 1].Z;
                            SamplesUpDown[i][l - 1, m, n + 1].Z = -SamplesUpDown[i][l - 1, m, n + 1].Z;
                        }
                    }
                }
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocet bodov vsetkych segmentov bocnej potahovej plochy
        private void ComputeSideSurface(List<Sphere> S, int lod)
        {
            Stopwatch stopWatch1 = new Stopwatch();
            Stopwatch stopWatch2 = new Stopwatch();

            if (Homotopy)
            {
                stopWatch1.Start();
                // prechadzame jednotlivymi segmentami
                for (int i = 0; i < S.Count; i++)
                {
                    /*Sphere S0 = S[i];
                    Sphere S1;
                    if (i != S.Count - 1) S1 = S[i + 1];
                    else S1 = S[0];

                    Vector3 t = S1.Center - S0.Center;
                    Vector3 X = S0.Center + (float)slider6.Value * t / 100.0f;
                    DrawNormalS(i, (float)slider6.Value / 100.0f, X.X, X.Y, S);*/


                    // vypocet vo vzorkovacich bodoch na "okrajoch" hornej casti plochy
                    for (int j = 0; j < lod; j++)
                    {
                        float u = (float)j / (lod - 1.0f);

                        /*Vector3 X = S0.Center + u * t;
                        Vector3 SX = SurfacePoint(X.Xy, S);*/

                        // vypocitame ich ako body na Coonsovej zaplate
                        for (int k = 0; k < lod; k++)
                        {
                            float v = (float)k / (lod - 1.0f);
                            //float theta = v * (float)Math.PI;
                            //Pts[k, j] = M(SX, theta, S0, S1);
                            //if (RozvetvenaHomotopia.IsChecked == true) Samples[i][k, j] = SSide(i, u, v, casT);
                            //else
                            //{
                            //    slider5.Value = 100.0;
                            Samples[i][k, j] = CoonsPatchPoint(i, u, v, casT, S);
                            //}
                        }
                    }
                }
                stopWatch1.Stop();
                casHom += stopWatch1.ElapsedMilliseconds;
            }
            else
            {
                stopWatch2.Start();
                // prechadzame jednotlivymi segmentami
                for (int i = 0; i < S.Count; i++)
                {
                    // vypocet vo vzorkovacich bodoch na "okrajoch" hornej casti plochy
                    slider5.Value = 100.0;
                    for (int j = 0; j < lod; j++)
                    {
                        float u = (float)j / (lod - 1.0f);

                        // vypocitame ich ako body na Coonsovej zaplate
                        for (int k = 0; k < lod; k++)
                        {
                            float v = (float)k / (lod - 1.0f);
                            Samples[i][k, j] = CoonsPatchPoint(i, u, v, S);
                        }
                    }
                }
                stopWatch2.Stop();
                casRozv += stopWatch2.ElapsedMilliseconds;
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocet a vykreslenie bodov vsetkych segmentov bocnej potahovej plochy
        private void DrawSideSurface(List<Sphere> S, int lod)
        {
            float[] red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float[] Diffuse = { 1.0f, 1.0f, 0.0f, 1.0f }, Ambient, Specular = { 0.5f, 0.5f, 0.5f, 1.0f };
            float Shininess = 0.7f;

            if (ColorOne)
            {
                Ambient = new float[] { 1.0f, 1.0f, 0.0f, 1.0f };
                Specular = new float[] { 0.05f, 0.05f, 0.05f, 0.5f };
                Shininess = 0.1f;
            }
            else
            {
                Ambient = new float[] { 1.0f, 0.55f, 0.0f, 1.0f };
                Specular = new float[] { 0.6f, 0.6f, 0.3f, 1.0f };
            }

            // prechadzame jednotlivymi segmentami
            for (int i = 0; i < S.Count; i++)
            {
                Sphere S0 = S[i];
                Sphere S1;
                if (i != S.Count - 1) S1 = S[i + 1];
                else S1 = S[0];

                Vector3 t = S1.Center - S0.Center;
                List<Vector3> N0 = CheckKonvexAt(i, 0, S);
                List<Vector3> N1 = CheckKonvexAt(i, 1, S);
                Vector3 X = HermiteCurve((float)slider6.Value / 100.0f, S0.Center, N0[1], N1[0], S1.Center); //S0.Center + (float)slider6.Value * t / 100.0f;

                DrawNormalS(i, (float)slider6.Value / 100.0f, (float)slider4.Value / 100.0f, X.X, X.Y, S);

                // vykreslenie bocnej casti plochy
                for (int j = 0; j < lod - 1; j++)
                {
                    for (int k = 0; k < lod - 1; k++)
                    {
                        if (checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                            // vypocet normal vo vrcholoch
                            Vector3 n1 = ComputeNormal(i, j, k).Normalized();
                            Vector3 n2 = ComputeNormal(i, j + 1, k).Normalized();
                            Vector3 n3 = ComputeNormal(i, j + 1, k + 1).Normalized();
                            Vector3 n4 = ComputeNormal(i, j, k + 1).Normalized();

                            GL.Begin(PrimitiveType.Quads);
                            GL.Normal3(n1);
                            GL.Vertex3(Samples[i][j, k]);
                            GL.Normal3(n2);
                            GL.Vertex3(Samples[i][j + 1, k]);
                            GL.Normal3(n3);
                            GL.Vertex3(Samples[i][j + 1, k + 1]);
                            GL.Normal3(n4);
                            GL.Vertex3(Samples[i][j, k + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(Samples[i][j, k]);
                            GL.Vertex3(Samples[i][j + 1, k]);
                            GL.Vertex3(Samples[i][j + 1, k + 1]);
                            GL.Vertex3(Samples[i][j, k + 1]);
                            GL.End();
                        }
                    }
                }
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vypocet celej potahovej plochy
        private void ComputeSkinningSurface(List<Sphere> S, int lod1, int lod2)
        {
            // ak je zvolena rozvetvena konstrukcia tak pocitame hornu/dolnu a bocnu cast
            if (rozvetvena.IsChecked == true || RozvetvenaHomotopia.IsChecked == true)
            {
                // vypocet taziska ktory pouzijeme na rozdelenie definicnej oblasti na trojuholniky 
                Vector3 T = new Vector3(0.0f, 0.0f, 0.0f);
                for (int i = 0; i < S.Count; i++)
                {
                    T += S[i].Center / S.Count;
                }

                if (RozvetvenaHomotopia.IsChecked == true)
                {
                    for (int i = 0; i < S.Count; i++)
                    {
                        Spheres[i].R0 = rRef;
                    }
                }
                else
                {
                    for (int i = 0; i < S.Count; i++)
                    {
                        Spheres[i].R0 = Spheres[i].R;
                    }
                }

                if(Homotopy) casHom = 0.0f;
                else casRozv = 0.0f;

                ComputeUpDownSurface(S, T, lod2);
                ComputeSideSurface(S, lod1);

                textBox1.Text = "Čas výpočtu 1. metódou: " + (casRozv / 1000.0f).ToString() + " s" + "\n" + "Čas výpočtu 2. metódou: " + (casHom / 1000.0f).ToString() + " s";

                if (Homotopy) casHom = 0.0f;
                else casRozv = 0.0f;
            }
            // ak je zvolena tubularna konstrukcia tak pocitame body tubularnej potahovej plochy
            if (tubularna.IsChecked == true && Spheres.Count > 1)
            {
                ComputeTubularSurface(lod1);
            }
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vykreslovanie celej potahovej plochy
        private void DrawSkinningSurface( List<Sphere> S, int lod1, int lod2)
        {
            // ak je zvolena rozvetvena konstrukcia tak kreslime hornu/dolnu a bocnu cast
            if (rozvetvena.IsChecked == true || RozvetvenaHomotopia.IsChecked == true)
            {
                if (checkBox6.IsChecked == true) DrawUpDownSurface(S, lod2);
                if (checkBox5.IsChecked == true) DrawSideSurface(S, lod1);
            }
            // ak je zvolena tubularna konstrukcia tak kreslime body tubularnej potahovej plochy
            if (tubularna.IsChecked == true)
            {
                DrawTubularSurface(lod1);
            }
        }

        #endregion

//-----------------------------------------------------------------------------------------------------------------------

        #region OVLADANIE UZIVATELSKEHO PROSTREDIA 

        /////////////////////////////////////////////////////////
        //                                                     //
        //       OVLADANIE UZIVATELSKEHO PROSTREDIA            //
        //                                                     //
        /////////////////////////////////////////////////////////

//-----------------------------------------------------------------------------------------------------------------------

        // vykreslovanie sceny
        private void GLControl_Paint(object sender, PaintEventArgs e)
        {
            if(BielaFarba.IsChecked == true) GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
            else GL.ClearColor(0.25f, 0.25f, 0.25f, 1.0f);

            // Modelview matica
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadIdentity();

            Matrix4 matLook = Matrix4.LookAt((float)(Dist * Math.Cos(Theta) * Math.Cos(Phi)), (float)(Dist * Math.Cos(Theta) * Math.Sin(Phi)), (float)(Dist * Math.Sin(Theta)), 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
            GL.LoadMatrix(ref matLook);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            if (perspective.IsChecked == true)
            {
                // perspektivna projekcia
                Matrix4 matPers = Matrix4.CreatePerspectiveFieldOfView(0.785f, (float)glControl.Width / (float)glControl.Height, 0.1f, 10.0f);
                GL.LoadMatrix(ref matPers);
            }
            else
            {
                // ortogonalna projekcia
                Matrix4 matOrtho = Matrix4.CreateOrthographic(Dist * (float)glControl.Width / (float)glControl.Height, Dist, -10.0f, 10.0f);//CreatePerspectiveFieldOfView(0.785f, (float)glControl.Width / (float)glControl.Height, 0.1f, 10.0f);
                GL.LoadMatrix(ref matOrtho);
            }
            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // vykreslenie suradnicovych osi a roviny z=0
            if (checkBox1.IsChecked == true) DrawAxes();
            if (checkBox.IsChecked == true) DrawGrid();
            if (ShowSurface && tubularna.IsChecked == true && (UCurves.IsChecked == true || VCurves.IsChecked == true)) DrawTubularIsoLines((int)slider7.Value, (int)slider8.Value);
            if (tubularna.IsChecked == true && ShowTubularNormals.IsChecked == true) DrawTubularNormals();

            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.DepthTest);

            // zobrazime aktualne hodnoty parametrov
            label1.Content = Lod1 * Lod1;
            label3.Content = Eta;
            label5.Content = (Lod2 + 2) * (Lod2 + 1) / 2;
            label7.Content = Delta;
            label8.Content = "Tau = " + Convert.ToString(Tau);
            label9.Content = "Lambda = " + Convert.ToString(Lambda);
            CasT.Content = "t = " + Convert.ToString(casT);
            label10.Content = "reper v c1("+Convert.ToString((float)slider6.Value / 100.0f) +")";
            label11.Content = "reper v dj(" + Convert.ToString((float)slider4.Value / 100.0f) + ")";
            label14.Content = (int)slider7.Value;
            label15.Content = (int)slider8.Value;
            label18.Content = alpha;
            SpheresSamples.Content = (int)SphereSample.Value;

            // nakreslime potahovu plochu podla zvolenej konstrukcie
            if (ShowSurface && Spheres.Count > 1 && !CreatingSphere && ActiveSphere == -1) DrawSkinningSurface(Spheres, Lod1, Lod2);
            if (ShowSurface && Spheres.Count > 1 && tubularna.IsChecked == true) DrawTubularSurface(Lod1);

            // ak je zapnute zobrazovanie vytvorenych sfer
            if (checkBox2.IsChecked == true)
            {
                for (int i = 0; i < Spheres.Count; i++)
                {
                    Spheres[i].Draw((int)SphereSample.Value, alpha, false);
                }
            }

            GL.Disable(EnableCap.Lighting);

            // prehodime buffre, aby sa vykreslila scena
            glControl.SwapBuffers();
        }

//-----------------------------------------------------------------------------------------------------------------------

        // zobrazi info o sfere nad ktorou sa prave nachadza mys
        private void ShowInfoAboutPoint(Vector2 mouse)
        {
            Vector3 H = FindIntersection(Spheres, new Vector2(mouse.X, mouse.Y));
            if(HitIndex != -1)
            {
                if (ActiveSphere == -1) Cursor = System.Windows.Input.Cursors.Hand;
                if (Keyboard.IsKeyDown(Key.M)) Cursor = System.Windows.Input.Cursors.SizeAll;
                if (Keyboard.IsKeyUp(Key.M)) Cursor = System.Windows.Input.Cursors.Hand;
                textBox.Text = "Sféra " + (HitIndex + 1).ToString() + ";  " + "stred: " + Spheres[HitIndex].Center.ToString() + ";  " + "polomer: " + Spheres[HitIndex].R.ToString();
            }
            /*else if (HitIndex == -1)
            {
                if(sphere.Count > 1)
                {
                    H = FindIntersection(sphere, normal, 0.05f, new Vector2(mouse.X, mouse.Y));
                    if (HitNormal != -1)
                    {
                        Cursor = System.Windows.Input.Cursors.Hand;
                        textBox.Text = "Riadiacy vektor " + (HitNormal + 1).ToString() + "\n" + "\n" + "n" + (HitNormal + 1).ToString() + " = " + normal[HitNormal].ToString();
                    }
                }
            }*/
            else
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
                textBox.Text = "";
            }
            //HitNormal = -1;
            HitIndex = -1;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void GLControl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ShowInfoAboutPoint(new Vector2(e.X, e.Y)); // zobrazime info o bode

            if (e.Button == MouseButtons.Right) // pravym tlacidlom ovladame kameru
            {
                IsRightDown = true;
                RightX = e.X;
                RightY = e.Y;
                dPhi = Phi;
                dTheta = Theta;
            }
            else if (e.Button == MouseButtons.Left) // lavym tlacidlom hladame na ktoru sferu (normalovy vektor ni) ukazuje kurzor
            {
                CreatingSphere = true;

                Vector3 start, end;

                int[] viewport = new int[4];
                Matrix4 modelMatrix, projMatrix;

                GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
                GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
                GL.GetInteger(GetPName.Viewport, viewport);

                start = UnProject(new Vector3(e.X, e.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
                end = UnProject(new Vector3(e.X, e.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

                float t;
                Vector3 center = new Vector3();
                if (end.Z != start.Z)
                {
                    t = -start.Z / (end.Z - start.Z);
                    center = start + t * (end - start);
                }
                else if (start.Z != 0)
                {
                    center.X = start.X;
                    center.Y = start.Y;
                    center.Z = 0.0f;
                }
                else center = start;

                HitPoint = FindIntersection(Spheres, new Vector2(e.X, e.Y));

                if (HitIndex != -1 && checkBox2.IsChecked == true)
                {
                    ActiveSphere = HitIndex;
                    if (Keyboard.IsKeyDown(Key.M))
                    {
                        Cursor = System.Windows.Input.Cursors.SizeAll;
                        Spheres[ActiveSphere].Center = center;
                    }
                    else Spheres[ActiveSphere].R = (Spheres[ActiveSphere].Center - HitPoint).Length;
                    CreatingSphere = false;
                }
                else if (Spheres.Count > 1 && ShowTubularNormals.IsChecked == true)
                {
                    HitPoint = FindIntersection(Spheres, Normals, 0.05f, new Vector2(e.X, e.Y));
                    if(HitNormal != -1)
                    {
                        CreatingSphere = false;
                        HitIndex = -1;
                        ActiveSphere = -1;
                    }
                }

                float[] diffuse = { 0.0f, 0.0f, 1.0f, 1.0f };

                if (CreatingSphere)
                {
                    Spheres.Add(new Sphere(center, 0.1f, diffuse));
                    ComputeTubularNormals(); 
                    Samples.Add(new Vector3[120, 120]);
                    SamplesUpDown.Add(new Vector3[41, 41, 41]);
                    RightX = e.X;
                    RightY = e.Y;
                }
                if (checkBox7.IsChecked == true) ComputeSkinningSurface(Spheres, Lod1, Lod2);
            }
           
            // prekresli scenu
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void GLControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ShowInfoAboutPoint(new Vector2(e.X, e.Y)); // zobrazime info o bode

            if (IsRightDown) //pri stlaceni praveho tlacidla otacame kameru
            {
                IsRightDown = true;

                Phi = dPhi + (RightX - e.X) / 200.0f;
                Theta = dTheta + (e.Y - RightY) / 200.0f;
            }
            else if (CreatingSphere) // lavym tlacidlom ovladame sfery
            {
                CreatingSphere = true;

                Vector3 start, end;

                int[] viewport = new int[4];
                Matrix4 modelMatrix, projMatrix;

                GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
                GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
                GL.GetInteger(GetPName.Viewport, viewport);

                start = UnProject(new Vector3(e.X, e.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
                end = UnProject(new Vector3(e.X, e.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

                float t;
                Vector3 P = new Vector3();
                if (end.Z != start.Z)
                {
                    t = -start.Z / (end.Z - start.Z);
                    P = start + t * (end - start);
                }
                else if (start.Z != 0)
                {
                    P.X = start.X;
                    P.Y = start.Y;
                    P.Z = 0.0f;
                }
                else P = start;

                Spheres[Spheres.Count - 1].R = (P - Spheres[Spheres.Count - 1].Center).Length;

                if (checkBox7.IsChecked == true && tubularna.IsChecked == true) ComputeTubularSurface(Lod1);

                RightY = e.Y;
                RightX = e.X;
            }
            else if(ActiveSphere != -1)
            {
                CreatingSphere = false;
                Vector3 start, end;

                int[] viewport = new int[4];
                Matrix4 modelMatrix, projMatrix;

                GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
                GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
                GL.GetInteger(GetPName.Viewport, viewport);

                start = UnProject(new Vector3(e.X, e.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
                end = UnProject(new Vector3(e.X, e.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

                float t;
                Vector3 P = new Vector3();
                if (end.Z != start.Z)
                {
                    t = -start.Z / (end.Z - start.Z);
                    P = start + t * (end - start);
                }
                else if (start.Z != 0)
                {
                    P.X = start.X;
                    P.Y = start.Y;
                    P.Z = 0.0f;
                }
                else P = start;

                if (Keyboard.IsKeyDown(Key.M))
                {
                    Cursor = System.Windows.Input.Cursors.SizeAll;
                    Spheres[ActiveSphere].Center += P - Spheres[ActiveSphere].Center;
                }
                else Spheres[ActiveSphere].R = (P - Spheres[ActiveSphere].Center).Length;

                if (checkBox7.IsChecked == true && tubularna.IsChecked == true) ComputeTubularSurface(Lod1);

                RightY = e.Y;
                RightX = e.X;
            }
            else if(HitNormal != -1)
            {
                CreatingSphere = false;
                ActiveSphere = -1;
                Vector3 start, end;

                int[] viewport = new int[4];
                Matrix4 modelMatrix, projMatrix;

                GL.GetFloat(GetPName.ModelviewMatrix, out modelMatrix);
                GL.GetFloat(GetPName.ProjectionMatrix, out projMatrix);
                GL.GetInteger(GetPName.Viewport, viewport);

                start = UnProject(new Vector3(e.X, e.Y, 0.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));
                end = UnProject(new Vector3(e.X, e.Y, 1.0f), projMatrix, modelMatrix, new Size(viewport[2], viewport[3]));

                float t;
                Vector3 P = new Vector3();
                if (end.Z != start.Z)
                {
                    t = -start.Z / (end.Z - start.Z);
                    P = start + t * (end - start);
                }
                else if (start.Z != 0)
                {
                    P.X = start.X;
                    P.Y = start.Y;
                    P.Z = 0.0f;
                }
                else P = start;

                Normals[HitNormal] = P - Spheres[HitNormal].Center;

                if (checkBox7.IsChecked == true && tubularna.IsChecked == true) ComputeTubularSurface(Lod1);

                RightY = e.Y;
                RightX = e.X;
            }

            // prekresli scenu
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void GLControl_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) IsRightDown = false;
            if (e.Button == MouseButtons.Left)
            {
                SetRefPoint(Spheres);
                if (checkBox7.IsChecked == true) ComputeSkinningSurface(Spheres, Lod1, Lod2);
                CreatingSphere = false;
                ActiveSphere = -1;
                HitIndex = -1;
                HitNormal = -1;
            }
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // pri dvojkliknuti mazeme oznacenu sferu S[i], jej prislusny vektor n[i] a vzorkovacie body
        private void GLControl_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Vector3 HP = FindIntersection(Spheres, new Vector2(e.X, e.Y));
                if (HitIndex != -1)
                {
                    Spheres.RemoveAt(HitIndex);
                    Samples.RemoveAt(HitIndex);
                    SamplesUpDown.RemoveAt(HitIndex);
                    if (Normals.Count > 1) Normals.RemoveAt(HitIndex);
                    if (Spheres.Count == 1) Normals.Clear();
                    HitIndex = -1;
                    SetRefPoint(Spheres);
                }
                // prekresli scenu
                glControl.Invalidate();
            }
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // zabezpeci zoomovanie
        private void GLControl_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            Dist -= e.Delta * 0.001f; // zmeni sa vzdialenost kamery od pociatku suradnicovej sustavy

            // prekresli scenu
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------
        
        // zabezpeci pohlady zhora a zbokov
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.X)
            {
                Phi = 0.0f; Theta = 0.0f; Dist = 5.0f;
            }
            if (e.Key == Key.Y)
            {
                Phi = 3.0f * (float)Math.PI / 2.0f; Theta = 0.0f; Dist = 5.0f;
            }
            if (e.Key == Key.Z)
            {
                Phi = 0.0f; Theta = (float)Math.PI / 2.0f; Dist = 5.0f;
            }
            // prekresli scenu z noveho pohladu
            glControl.Invalidate();
        }
        
//-----------------------------------------------------------------------------------------------------------------------

        private void rozvetvena_Checked(object sender, RoutedEventArgs e)
        {
            casT = 1.0f;
            Homotopy = false;
            if (ShowSurface) ComputeSkinningSurface(Spheres, Lod1, Lod2);
            ShowTubular = false;

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void tubularna_Checked(object sender, RoutedEventArgs e)
        {
            casT = 1.0f;
            slider5.Value = 100;
            ComputeTubularSurface(Lod1);
            ShowTubular = true;

            glControl.Invalidate();
        }

        private void tubularna_Unchecked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void RozvetvenaHomotopia_Checked(object sender, RoutedEventArgs e)
        {
            // vypocet taziska ktory pouzijeme na rozdelenie definicnej oblasti na trojuholniky 
            /*Vector3 T = new Vector3(0.0f, 0.0f, 0.0f);
            for (int i = 0; i < Spheres.Count; i++)
            {
                T += Spheres[i].Center / Spheres.Count;
            }

            ComputeUpDownSurface(Spheres, T, Lod2);
            ComputeSideSurface(Spheres, Lod1);*/
            Homotopy = true;
            SetRefPoint(Spheres);

            if (ShowSurface) ComputeSkinningSurface(Spheres, Lod1, Lod2);
            ShowTubular = false;

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void RozvetvenaHomotopia_Unchecked(object sender, RoutedEventArgs e)
        {
            Homotopy = false;
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Lod1 = (int)slider.Value;
            if (ShowTubular) ComputeTubularSurface(Lod1);
            else if (ShowSurface) ComputeSideSurface(Spheres, Lod1);

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Eta = (int)slider1.Value;
            if (ShowSurface) ComputeSkinningSurface(Spheres, Lod1, Lod2);

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void slider2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Lod2 = (int)slider2.Value;

            if (ShowSurface && checkBox6.IsChecked == true && (rozvetvena.IsChecked == true || RozvetvenaHomotopia.IsChecked == true))
            {
                Vector3 T = new Vector3(0.0f, 0.0f, 0.0f);
                for (int i = 0; i < Spheres.Count; i++)
                {
                    T += Spheres[i].Center / Spheres.Count;
                }
                ComputeUpDownSurface(Spheres, T, Lod2);
            }

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void slider3_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Delta = (int)slider3.Value / 100.0f;
            /*float d = FindMaxDelta();
            if (d < 1 && Delta > d) Delta = d;
            if (!CheckDelta())
            {
                Delta = FindMaxDelta();
                slider3.Value = (double)Delta * 100;
            }*/
            if (ShowSurface) ComputeSkinningSurface(Spheres, Lod1, Lod2);

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void tau_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Tau = 5.0f * (int)tau.Value / 100.0f;
            if (ShowSurface) ComputeSideSurface(Spheres, Lod1);

            glControl.Invalidate();
        }

        private void lambda_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Lambda = 5.0f * (int)lambda.Value / 100.0f;
            if (ShowSurface) ComputeSideSurface(Spheres, Lod1);

            glControl.Invalidate();
        }
//-----------------------------------------------------------------------------------------------------------------------

        private void ModifShep_Checked(object sender, RoutedEventArgs e)
        {
            if (ShowSurface) ComputeSkinningSurface(Spheres, Lod1, Lod2);

            glControl.Invalidate();
        }

        private void TubularHomotopy_Checked(object sender, RoutedEventArgs e)
        {
            casT = 1.0f;
            slider5.Value = 100;
            ComputeTubularSurface(Lod1);

            glControl.Invalidate();
        }

        private void slider9_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            alpha = (float)slider9.Value / 100.0f;

            glControl.Invalidate();
        }

        private void radioButton_Checked(object sender, RoutedEventArgs e)
        {
            ColorOne = true;

            glControl.Invalidate();
        }

        private void radioButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ColorOne = false;

            glControl.Invalidate();
        }

        private void perspective_Checked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

        private void perspective_Unchecked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

        private void slider4_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void slider6_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            glControl.Invalidate();
        }
    
//-----------------------------------------------------------------------------------------------------------------------

        private void slider5_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            casT = (int)slider5.Value / 100.0f;
            if (ShowSurface && RozvetvenaHomotopia.IsChecked == true) ComputeSkinningSurface(Spheres, Lod1, Lod2);

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void checkBox7_Checked(object sender, RoutedEventArgs e)
        {
            ShowSurface = true;
            ComputeSkinningSurface(Spheres, Lod1, Lod2);

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void checkBox7_Unchecked(object sender, RoutedEventArgs e)
        {
            ShowSurface = false;

            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        private void ShowTubularNormals_Checked(object sender, RoutedEventArgs e)
        {
            glControl.Invalidate();
        }

//-----------------------------------------------------------------------------------------------------------------------

        // vymaze sfery, normaly a vsetky vzorkovacie body
        private void button_Click(object sender, RoutedEventArgs e)
        {
            Spheres.Clear();
            SamplesUpDown.Clear();
            Samples.Clear();
            Normals.Clear();
            IsRightDown = false;
            ShowSurface = false;
            checkBox7.IsChecked = false;
            ActiveSphere = -1;
            HitIndex = -1;
            casT = 1.0f;
            glControl.Invalidate();
        }

        #endregion
    }

    // trieda sfera dana stredom a polomerom, dodatocny param. je jej difuzna farba
    class Sphere
    {
        public float R, R0;
        public Vector3 Center;
        public float[] DiffColor;

        public Sphere(Vector3 center, float r, float[] diffColor)
        {
            Center = center;
            R = r;
            R0 = R;
            DiffColor = diffColor;
        }

        // ak nezvolime farbu tak defaultne nastavime na cervenu
        public Sphere(Vector3 center, float r)
        {
            Center = center;
            R = r;
            R0 = R;
            DiffColor = new float[] { 1.0f, 0.0f, 0.0f, 1.0f };
        }

        public Matrix4 M(float t)
        {
            float dt = 1.0f + (1.0f - t) * (R0 / R - 1.0f);
            Vector4 row0 = new Vector4(dt, 0, 0, Center.X * (1.0f - dt));
            Vector4 row1 = new Vector4(0, dt, 0, Center.Y * (1.0f - dt));
            Vector4 row2 = new Vector4(0, 0, dt, 0);
            Vector4 row3 = new Vector4(0, 0, 0, 1.0f);

            return new Matrix4(row0, row1, row2, row3);
        }

        public void Draw(int Lod, float Alpha, bool drawNet)
        {
            // parametre pre parametrizaciu sfery
            float phi, theta;

            // difuzna farba droteneho modelu sfery 
            float[] diffNet = { 0.0f, 0.0f, 0.0f, 1.0f };

            //spekularna farba sfery
            float[] Ambient, Specular;
            float Shininess;

            if (MainWindow.ColorOne)
            {
                DiffColor = new float[] { 0.0f, 0.0f, 1.0f, Alpha };
                Ambient = new float[] { 0.0f, 0.0f, 1.0f, Alpha };
                Specular = new float[] { 0.1f, 0.1f, 0.1f, 0.5f };
                Shininess = 0.1f;
            }
            else
            {
                DiffColor = new float[] { 0.0f, 1.0f, 1.0f, Alpha };
                Ambient = new float[] { 0.0f, 0.2f, 0.5f, Alpha };
                Specular = new float[] { 0.75f, 0.75f, 0.75f, Alpha / 2.0f };
                Shininess = 0.7f;
            }

            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, DiffColor);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

            float h1 = (float)(Math.PI / Lod);
            float h2 = 2 * (float)(Math.PI / Lod);

            for (int i = 0; i < Lod; i++)
            {
                theta = i * (float)(Math.PI / Lod) - (float)(Math.PI / 2.0f);
                for (int j = 0; j < Lod; j++)
                {
                    phi = 2 * (float)(Math.PI * j / Lod);

                    Vector3 P1, P2, P3, P4;

                    if (MainWindow.Homotopy)
                    {
                        /*Matrix4 N = M(MainWindow.casT);
                        P1 = (N * new Vector4(P1, 1.0f)).Xyz;
                        P2 = (N * new Vector4(P2, 1.0f)).Xyz;
                        P3 = (N * new Vector4(P3, 1.0f)).Xyz;
                        P4 = (N * new Vector4(P4, 1.0f)).Xyz;*/
                        float dt = 1.0f + (1.0f - MainWindow.casT) * (R0 / R - 1.0f);
                        P1 = new Vector3(Center.X - R * dt * (float)Math.Cos(theta) * (float)Math.Cos(phi), Center.Y - R * dt * (float)Math.Cos(theta) * (float)Math.Sin(phi), Center.Z - R * dt * (float)Math.Sin(theta));
                        P2 = new Vector3(Center.X - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi), Center.Y - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi), Center.Z - R * dt * (float)Math.Sin(theta + h1));
                        P3 = new Vector3(Center.X - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi + h2), Center.Y - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi + h2), Center.Z - R * dt * (float)Math.Sin(theta + h1));
                        P4 = new Vector3(Center.X - R * dt * (float)Math.Cos(theta) * (float)Math.Cos(phi + h2), Center.Y - R * dt * (float)Math.Cos(theta) * (float)Math.Sin(phi + h2), Center.Z - R * dt * (float)Math.Sin(theta));
                    }
                    else
                    {
                        P1 = new Vector3(Center.X - R * (float)Math.Cos(theta) * (float)Math.Cos(phi), Center.Y - R * (float)Math.Cos(theta) * (float)Math.Sin(phi), Center.Z - R * (float)Math.Sin(theta));
                        P2 = new Vector3(Center.X - R * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi), Center.Y - R * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi), Center.Z - R * (float)Math.Sin(theta + h1));
                        P3 = new Vector3(Center.X - R * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi + h2), Center.Y - R * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi + h2), Center.Z - R * (float)Math.Sin(theta + h1));
                        P4 = new Vector3(Center.X - R * (float)Math.Cos(theta) * (float)Math.Cos(phi + h2), Center.Y - R * (float)Math.Cos(theta) * (float)Math.Sin(phi + h2), Center.Z - R * (float)Math.Sin(theta));
                    }

                    Vector3 n1 = (P1 - Center).Normalized();
                    Vector3 n2 = (P2 - Center).Normalized();
                    Vector3 n3 = (P3 - Center).Normalized();
                    Vector3 n4 = (P4 - Center).Normalized();

                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, DiffColor);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);
                    GL.Begin(PrimitiveType.Quads);
                    GL.Normal3(n1.X, n1.Y, n1.Z);
                    GL.Vertex3(P1.X, P1.Y, P1.Z);
                    GL.Normal3(n2.X, n2.Y, n2.Z);
                    GL.Vertex3(P2.X, P2.Y, P2.Z);
                    GL.Normal3(n3.X, n3.Y, n3.Z);
                    GL.Vertex3(P3.X, P3.Y, P3.Z);
                    GL.Normal3(n4.X, n4.Y, n4.Z);
                    GL.Vertex3(P4.X, P4.Y, P4.Z);
                    GL.End();

                    if (drawNet)
                    {
                        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, diffNet);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, diffNet);
                        GL.LineWidth(1.0f);
                        GL.Begin(PrimitiveType.Lines);
                        GL.Normal3(n1.X, n1.Y, n1.Z);
                        GL.Vertex3(P1.X, P1.Y, P1.Z);
                        GL.Normal3(n2.X, n2.Y, n2.Z);
                        GL.Vertex3(P2.X, P2.Y, P2.Z);

                        GL.Normal3(n1.X, n1.Y, n1.Z);
                        GL.Vertex3(P1.X, P1.Y, P1.Z);
                        GL.Normal3(n4.X, n4.Y, n4.Z);
                        GL.Vertex3(P4.X, P4.Y, P4.Z);
                        GL.End();
                    }
                }
            }

        }
    }
}