using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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

        public static float eps = 0.00001f;

        float step = 0.01f;
        public static Vector3 e3 = new Vector3(0, 0, 1);

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

        // demo
        Dictionary<int, List<Sphere>> SpheresBySideId = new Dictionary<int, List<Sphere>>();
        Dictionary<int, BranchedSurface> BranchedSurfacesBySideId = new Dictionary<int, BranchedSurface>();
        Dictionary<Tuple<int, int>, SideSurface> SideSurfacesByBranchedPairs = new Dictionary<Tuple<int, int>, SideSurface>();
        bool IsDemo = true;

        // hodnoty pouzite vo vahovych funkciach
        public static int Eta;
        float Delta;

        // ked budeme vytvarat novu sferu
        bool CreatingSphere = false;

        // ked chceme zobrazit potahovu plochu
        bool ShowSurface = false;

        // vzorkovacie body na jednotlivych segmentoch potahovej plochy
        public int Lod1, Lod2;
        List<Vector3[,,]> SamplesUp = new List<Vector3[,,]>();
        List<Vector3[,,]> SamplesDown = new List<Vector3[,,]>();
        List<Vector3[,]> Samples = new List<Vector3[,]>();

        // normalove vektory rovin, v ktorych lezia dotykove kruznice, pre tubularnu konstrukciu
        List<Vector3> Normals = new List<Vector3>();

        // index kliknuteho vektora ked ho budeme menit
        int HitNormal = -1;

        // ked chceme zobrazit tubularnu plochu
        bool ShowTubular = false;

        public static float Tau = 1.0f, Lambda = 1.0f;
        public static float casT = 1.0f;
        public static bool Homotopy = true;
        public static float alpha = 1.0f;
        public static bool ColorOne = false;
        public static bool NonCoplanarSpheres = true;

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
            glControl.KeyDown += GLControl_KeyDown;

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
            Lod2 = 30;

            // _CreateExample1();
            _CreateDodecahedron();
        }

        private void _CreateExample1()
        {
            _Clear();

            Spheres.Add(new Sphere(Spheres.Count, new Vector3(-1, 0, 0), 0.1f));
            ComputeTubularNormals();

            Spheres.Add(new Sphere(Spheres.Count, new Vector3(0, -1, 1), 0.3f));
            ComputeTubularNormals();

            Spheres.Add(new Sphere(Spheres.Count, new Vector3(1, 0, 0), 0.1f));
            ComputeTubularNormals();

            Spheres.Add(new Sphere(Spheres.Count, new Vector3(0, 1, 0.5f), 0.2f));
            ComputeTubularNormals();

            _AddSamples(4);
        }

        private void _CreateDodecahedron()
        {
            _Clear();

            _MakeDodecahedronSpheres(1.0f);

            //// Size parameter: This is distance of each vector from origin
            //float r = (float)Math.Sqrt(3);
            //float sphereRadius = 0.1f;

            //IList<Vector3> vertices = _MakeDodecahedron(r);
            //foreach (Vector3 vertex in vertices)
            //{
            //    Spheres.Add(new Sphere(vertex, sphereRadius));
            //}
            //_AddSamples(Spheres.Count);
        }

        private void _Clear()
        {
            Spheres.Clear();
            SamplesUp.Clear();
            SamplesDown.Clear();
            Samples.Clear();
            Normals.Clear();
            SpheresBySideId.Clear();
            BranchedSurfacesBySideId.Clear();
            SideSurfacesByBranchedPairs.Clear();
        }

        /// <summary>
        /// Generates a list of vertices (in arbitrary order) for a tetrahedron centered on the origin.
        /// </summary>
        /// <param name="r">The distance of each vertex from origin.</param>
        /// <returns></returns>
        private static IList<Vector3> _MakeDodecahedron(float r)
        {
            // Calculate constants that will be used to generate vertices
            float phi = (float)(Math.Sqrt(5) - 1) / 2; // The golden ratio

            float a = (float)(1 / Math.Sqrt(3));
            float b = a / phi;
            float c = a * phi;

            // Generate each vertex
            List<Vector3> vertices = new List<Vector3>();
            foreach (float i in new[] { -1.0f, 1.0f })
            {
                foreach (float j in new[] { -1.0f, 1.0f })
                {
                    vertices.Add(new Vector3(
                                        0,
                                        i * c * r,
                                        j * b * r));
                    vertices.Add(new Vector3(
                                        i * c * r,
                                        j * b * r,
                                        0));
                    vertices.Add(new Vector3(
                                        i * b * r,
                                        0,
                                        j * c * r));

                    foreach (float k in new[] { -1.0f, 1.0f })
                        vertices.Add(new Vector3(
                                            i * a * r,
                                            j * a * r,
                                            k * a * r));
                }
            }
            return vertices;
        }

        // Return the vertices for an dodecahedron.
        // http://www.csharphelper.com/howtos/howto_wpf_3d_platonic_dodecahedron.html
        private void _MakeDodecahedronSpheres(float side_length)
        {
            _Clear();

            // Value t1 is actually never used.
            float s = side_length;
            //double t1 = 2.0f * (float)Math.PI / 5.0f;
            float t2 = (float)Math.PI / 10.0f;
            float t3 = 3.0f * (float)Math.PI / 10.0f;
            float t4 = (float)Math.PI / 5.0f;
            float d1 = s / 2.0f / (float)Math.Sin(t4);
            float d2 = d1 * (float)Math.Cos(t4);
            float d3 = d1 * (float)Math.Cos(t2);
            float d4 = d1 * (float)Math.Sin(t2);
            float Fx =
                (s * s - (2.0f * d3) * (2.0f * d3) -
                    (d1 * d1 - d3 * d3 - d4 * d4)) /
                        (2.0f * (d4 - d1));
            float d5 = (float)Math.Sqrt(0.5f *
                (s * s + (2.0f * d3) * (2.0f * d3) -
                    (d1 - Fx) * (d1 - Fx) -
                        (d4 - Fx) * (d4 - Fx) - d3 * d3));
            float Fy = (Fx * Fx - d1 * d1 - d5 * d5) / (2.0f * d5);
            float Ay = d5 + Fy;

            Vector3 A = new Vector3(d1, Ay, 0);
            Vector3 B = new Vector3(d4, Ay, d3);
            Vector3 C = new Vector3(-d2, Ay, s / 2);
            Vector3 D = new Vector3(-d2, Ay, -s / 2);
            Vector3 E = new Vector3(d4, Ay, -d3);
            Vector3 F = new Vector3(Fx, Fy, 0);
            Vector3 G = new Vector3(Fx * (float)Math.Sin(t2), Fy,
                Fx * (float)Math.Cos(t2));
            Vector3 H = new Vector3(-Fx * (float)Math.Sin(t3), Fy,
                Fx * (float)Math.Cos(t3));
            Vector3 I = new Vector3(-Fx * (float)Math.Sin(t3), Fy,
                -Fx * (float)Math.Cos(t3));
            Vector3 J = new Vector3(Fx * (float)Math.Sin(t2), Fy,
                -Fx * (float)Math.Cos(t2));
            Vector3 K = new Vector3(Fx * (float)Math.Sin(t3), -Fy,
                Fx * (float)Math.Cos(t3));
            Vector3 L = new Vector3(-Fx * (float)Math.Sin(t2), -Fy,
                Fx * (float)Math.Cos(t2));
            Vector3 M = new Vector3(-Fx, -Fy, 0);
            Vector3 N = new Vector3(-Fx * (float)Math.Sin(t2), -Fy,
                -Fx * (float)Math.Cos(t2));
            Vector3 O = new Vector3(Fx * (float)Math.Sin(t3), -Fy,
                -Fx * (float)Math.Cos(t3));
            Vector3 P = new Vector3(d2, -Ay, s / 2);
            Vector3 Q = new Vector3(-d4, -Ay, d3);
            Vector3 R = new Vector3(-d1, -Ay, 0);
            Vector3 S = new Vector3(-d4, -Ay, -d3);
            Vector3 T = new Vector3(d2, -Ay, -s / 2);

            float radius = 0.1f;
            Dictionary<string, int> ids = new Dictionary<string, int>()
            {
                { "A", 0 },
                { "B", 1 },
                { "C", 2 },
                { "D", 3 },
                { "E", 4 },
                { "F", 5 },
                { "G", 6 },
                { "H", 7 },
                { "I", 8 },
                { "J", 9 },
                { "K", 10 },
                { "L", 11 },
                { "M", 12 },
                { "N", 13 },
                { "O", 14 },
                { "P", 15 },
                { "Q", 16 },
                { "R", 17 },
                { "S", 18 },
                { "T", 19 }
            };

            Spheres.Add(new Sphere(Spheres.Count, A, radius));
            Spheres.Add(new Sphere(Spheres.Count, B, radius));
            Spheres.Add(new Sphere(Spheres.Count, C, radius));
            Spheres.Add(new Sphere(Spheres.Count, D, radius));
            Spheres.Add(new Sphere(Spheres.Count, E, radius));
            Spheres.Add(new Sphere(Spheres.Count, F, radius));
            Spheres.Add(new Sphere(Spheres.Count, G, radius));
            Spheres.Add(new Sphere(Spheres.Count, H, radius));
            Spheres.Add(new Sphere(Spheres.Count, I, radius));
            Spheres.Add(new Sphere(Spheres.Count, J, radius));
            Spheres.Add(new Sphere(Spheres.Count, K, radius));
            Spheres.Add(new Sphere(Spheres.Count, L, radius));
            Spheres.Add(new Sphere(Spheres.Count, M, radius));
            Spheres.Add(new Sphere(Spheres.Count, N, radius));
            Spheres.Add(new Sphere(Spheres.Count, O, radius));
            Spheres.Add(new Sphere(Spheres.Count, P, radius));
            Spheres.Add(new Sphere(Spheres.Count, Q, radius));
            Spheres.Add(new Sphere(Spheres.Count, R, radius));
            Spheres.Add(new Sphere(Spheres.Count, S, radius));
            Spheres.Add(new Sphere(Spheres.Count, T, radius));

            Sphere sphereRef = _GetRefSphere(Spheres);

            int id = 0;
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["E"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["D"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["C"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["B"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["A"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            //-------------------------------------------------------------------------

            // 1
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["K"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["F"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["A"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["B"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["G"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["B"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["C"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["H"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["L"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["G"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            // 3
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["M"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["H"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["C"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["D"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["I"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            // 4
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["I"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["D"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["E"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["J"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["N"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["J"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["E"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["A"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["F"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["O"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            //-------------------------------------------------------------------------
            // 6
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["K"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["P"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["T"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["O"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["F"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["L"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["Q"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["P"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["K"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["G"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            // 8
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["M"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["R"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["Q"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["L"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["H"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            // 9
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["N"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["S"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["R"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["M"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["I"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["O"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["T"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["S"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["N"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["J"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));
            id++;

            //----------------------------------------------------------------------------

            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["S"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["T"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["P"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["Q"]]);
            GetOrAdd(SpheresBySideId, id, _ => new List<Sphere>()).Add(Spheres[ids["R"]]);
            BranchedSurfacesBySideId.Add(id, new BranchedSurface(this, id, SpheresBySideId[id], sphereRef, _GetMatrix(SpheresBySideId[id], e3, Math.PI)));

            List<Tuple<int, int>> pairs = new List<Tuple<int, int>>()
            {
                new Tuple<int, int>(0, 1),
                new Tuple<int, int>(0, 2),
                new Tuple<int, int>(1, 2),
                new Tuple<int, int>(0, 3),
                new Tuple<int, int>(2, 3),
                new Tuple<int, int>(0, 4),
                new Tuple<int, int>(3, 4),
                new Tuple<int, int>(5, 0),
                new Tuple<int, int>(4, 5),
                new Tuple<int, int>(1, 5),
                new Tuple<int, int>(1, 6), // Tato ma naopak normaly
                new Tuple<int, int>(5, 6),
                new Tuple<int, int>(1, 7),
                new Tuple<int, int>(2, 7),
                new Tuple<int, int>(6, 7),
                new Tuple<int, int>(2, 8),
                new Tuple<int, int>(3, 8), // Tato ma naopak normaly
                new Tuple<int, int>(7, 8),
                new Tuple<int, int>(3, 9),
                new Tuple<int, int>(4, 9), // Tato ma naopak normaly
                new Tuple<int, int>(8, 9),
                new Tuple<int, int>(4, 10),
                new Tuple<int, int>(5, 10),
                new Tuple<int, int>(10, 9),
                new Tuple<int, int>(6, 10),
                new Tuple<int, int>(6, 11),
                new Tuple<int, int>(7, 11),
                new Tuple<int, int>(8, 11),
                new Tuple<int, int>(9, 11),
                new Tuple<int, int>(10, 11)
            };

            foreach (var pair in pairs)
            {
                SideSurfacesByBranchedPairs.Add(pair, new SideSurface(this, SideSurfacesByBranchedPairs.Count, BranchedSurfacesBySideId[pair.Item1], BranchedSurfacesBySideId[pair.Item2]));
            }

            _AddSamples(Spheres.Count);
        }

        public static TV GetOrAdd<TK, TV>(IDictionary<TK, TV> source, TK key, Func<TK, TV> addFunc)
        {
            if (source.TryGetValue(key, out TV value))
            {
                return value;
            }
            source[key] = value = addFunc(key);
            return value;
        }

        private Matrix4 _GetMatrix(List<Sphere> spheres, Vector3 direction, double angleOffset = 0.0)
        {
            Vector3 direction1 = spheres[1].Center - spheres[2].Center;
            Vector3 direction2 = spheres[3].Center - spheres[2].Center;

            Vector3 normal = Vector3.Cross(direction1, direction2).Normalized();
            float angle = (float)(angleOffset + Math.Acos(Vector3.Dot(normal, direction) / (normal.Length * direction.Length)));

            // Based on http://www.gamedev.net/reference/articles/article1199.asp

            Vector3 axis = Vector3.Cross(normal, direction).Normalized();
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            float t = 1 - c;
            float x = axis.X, y = axis.Y, z = axis.Z;
            float tx = t * x, ty = t * y;

            Matrix4 matrix = new Matrix4(
                tx * x + c, tx * y - s * z, tx * z + s * y, 0,
                tx * y + s * z, ty * y + c, ty * z - s * x, 0,
                tx * z - s * y, ty * z + s * x, t * z * z + c, 0,
                0, 0, 0, 1
            );

            Vector3 point = (matrix * new Vector4(spheres[0].Center.X, spheres[0].Center.Y, spheres[0].Center.Z, 1)).Xyz;
            matrix.Row2.W = -point.Z;

            return matrix;
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
        public static int KDelta(int i, int j)
        {
            if (i == j) return 1;
            else return 0;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // i-ta vahova funkcia v bode X pri danej mnozine sfer S
        private static float w(int i, Vector2 X, List<Sphere> S, float delta)
        {
            for (int j = 0; j < S.Count; j++)
            {
                if ((X - S[j].Center.Xy).Length - delta * S[j].R <= eps) return KDelta(i, j); //if (Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2) <= eps) return KDelta(i, j);
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
        private static float p(int i, Vector2 X, List<Sphere> S, float delta)
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
        private Vector3 P(Vector2 X, List<Sphere> S, bool up)
        {
            /* Vector3 n = new Vector3(0.0f, 0.0f, 1.0f);
             Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
             Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);
             Vector3 pref = S[0].Center + S[0].R * n;*/
            if (up)
            {
                new Vector3(X.X, X.Y, S[0].R);
            }

            return new Vector3(X.X, X.Y, -S[0].R); //pref + (X.X - S[0].Center.X) * e1 + (X.Y - S[0].Center.Y) * e2;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vypocita bod na vrchnej potahovej ploche sfer S v bode X
        private Vector3 SurfacePoint(Vector2 X, List<Sphere> S, bool up)
        {
            Vector3 n = new Vector3(0.0f, 0.0f, 1.0f);
            return P(X, S, up) + R(X, S) * n;
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
        public static float H3(int i, float x)
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
        public static float dH3(int i, float x)
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

        private static List<Vector3> CheckKonvexAt(int i, int j, List<Sphere> S)
        {
            Vector3 t0 = new Vector3(), t1 = new Vector3();
            if (S.Count > 1)
            {
                if (i >= 1 && i <= S.Count - 3)
                {
                    t0 = S[i + j].CenterInTime(0) - S[i + j - 1].CenterInTime(0);
                    t1 = S[i + j + 1].CenterInTime(0) - S[i + j].CenterInTime(0);
                }
                else if (i == 0)
                {
                    if (j == 0)
                    {
                        t0 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                        t1 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                        if (S.Count > 2) t1 = S[2].CenterInTime(0) - S[1].CenterInTime(0);
                        else t1 = S[0].CenterInTime(0) - S[1].CenterInTime(0);
                    }
                }
                else if (i == S.Count - 2)
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 2].CenterInTime(0) - S[S.Count - 3].CenterInTime(0);
                        t1 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                        t1 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                    }
                }
                else
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                        t1 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                        t1 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                    }
                }

                if (det(t0.Xy, t1.Xy) < -eps)
                {
                    Vector3 s = (t0 + t1) / 2.0f;
                    t0 = s;
                    t1 = s;
                }
            }

            t0.Z = 0;
            t1.Z = 0;

            return new List<Vector3> { t0, t1 };
        }

        //-----------------------------------------------------------------------------------------------------------------------

        public static Vector3 HermiteCurve(float u, Vector3 A, Vector3 v0, Vector3 v1, Vector3 B)
        {
            return H3(0, u) * A + H3(1, u) * v0 + H3(2, u) * v1 + H3(3, u) * B;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        public static Vector3 dHermiteCurve(float u, Vector3 A, Vector3 v0, Vector3 v1, Vector3 B)
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

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[i + 1].CenterInTime(0));  //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
            else X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));  //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
            return SurfacePoint(X.Xy, S, true);
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

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Id, S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Id, S[0].Center, S[0].R);
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

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Id, S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Id, S[0].Center, S[0].R);
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

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[i + 1].CenterInTime(0));
            else X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));

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
                n = (S[i + 1].CenterInTime(0) - S[i].CenterInTime(0)).Normalized();
                //r2 = (float)Math.Exp(Spheres[i + 1].R);
            }
            else
            {
                S1 = S[0];
                n = (S[0].CenterInTime(0) - S[i].CenterInTime(0)).Normalized();
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
            if (S.Count > 2)
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
            if (j == 0) return (float)Math.Pow(S0.R / S1.R, Lambda) * n;
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
            return Sc(i, u, v, S) + Sd(i, u, v, S) - Scd(i, u, v, S);
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

            for (int i = 0; i < S.Count; i++)
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
                C1 = SurfacePoint(X, S, true);
                C0 = C1;
                C0.Z = -C0.Z;

                D0 = d0(i, s, S);
                D1 = d1(i, s, S);
            }
            else
            {
                C1 = SUp(u, v, casT, S, NonCoplanarSpheres);
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

            if (reperC.IsChecked == true)
            {
                GL.Vertex3(C1);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C1 + normalS(u, v, S));
                else GL.Vertex3(C1 + normalSH(u, v, casT, S));
            }

            if (reperD.IsChecked == true)
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

            if (t >= 0 && t <= 1 && reperC.IsChecked == true)
            {
                Vector3 dss = ds(i, t, S);
                Vector3 dss2 = dsup(i, t, casT, S);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, green);
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, green);
                GL.LineWidth(2.0f);
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(C1);
                if (rozvetvena.IsChecked == true) GL.Vertex3(C1 + dss);
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
            else if (i == 0)
            {
                if (j > 0 && j < Lod1 - 1)
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
            else */
            return SurfacePoint(X, S, true) + (t(i, u) - new Vector3(X.X, X.Y, 0.0f));
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
                Vector2 C0 = Spheres[j].CenterInTime(0).Xy;
                Vector2 C1;
                float d, t = -1;
                if (j < Spheres.Count - 1) C1 = Spheres[j + 1].CenterInTime(0).Xy; // d = det(Spheres[i + 1].Center.Xy - Spheres[i].Center.Xy, X - Spheres[i].Center.Xy);
                else C1 = Spheres[0].CenterInTime(0).Xy; //d = det(Spheres[0].Center.Xy - Spheres[i].Center.Xy, X - Spheres[i].Center.Xy);

                d = det(C1 - C0, X - C0);

                if (d <= eps && d >= -eps)
                {
                    if (C0.X != C1.X)
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
                Sum += (TanAlfaPol(j - 1, u, v) + TanAlfaPol(j, u, v)) / (Spheres[j].CenterInTime(0).Xy - X).Length;
            }

            return (TanAlfaPol(i - 1, u, v) + TanAlfaPol(i, u, v)) / ((Spheres[i].CenterInTime(0).Xy - X).Length * Sum);
        }

        private float TanAlfaPol(int i, float u, float v)
        {
            Vector2 X = new Vector2(u, v);
            Vector2 v0;
            Vector2 v1;
            if (i < 0) v0 = Spheres[Spheres.Count - 1].CenterInTime(0).Xy - X;
            else v0 = Spheres[i].CenterInTime(0).Xy - X;

            if (i < Spheres.Count - 1) v1 = Spheres[i + 1].CenterInTime(0).Xy - X;
            else v1 = Spheres[0].CenterInTime(0).Xy - X;

            return det(v0, v1) / (v0.Length * v1.Length + Vector2.Dot(v0, v1));
        }

        public static float det(Vector2 X, Vector2 Y)
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

        private Sphere _GetRefSphere(List<Sphere> S, bool min = false)
        {
            float rMin = float.MaxValue;
            float rMax = float.MinValue;
            int k = -1;

            for (int i = 0; i < S.Count; i++)
            {
                if (min && rMin > S[i].R)
                {
                    rMin = S[i].R;
                    k = i;
                }
                else if (rMax < S[i].R)
                {
                    rMax = S[i].R;
                    k = i;
                }
            }
            if (k != -1) return S[k];
            return null;
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

            if (ModifShep.IsChecked == true)
            {
                for (int i = 0; i < S.Count; i++)
                {
                    dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                    dt += w(i, X, S, 0.0f) * dit;
                    ut += w(i, X, S, 0.0f) * S[i].Center.X * (1.0f - dit);
                    vt += w(i, X, S, 0.0f) * S[i].Center.Y * (1.0f - dit);
                }
            }
            if (MeanValue.IsChecked == true)
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

        private Matrix4 K(float u, float v, float t, List<Sphere> S)
        {
            float dt = 0.0f;
            float ut = 0.0f, vt = 0.0f, zt = 0.0f, dit;

            Vector2 X = new Vector2(u, v);

            Vector4 row0 = Vector4.Zero;
            Vector4 row1 = Vector4.Zero;
            Vector4 row2 = Vector4.Zero;
            Vector4 row3 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            if (ModifShep.IsChecked == true)
            {
                for (int i = 0; i < S.Count; i++)
                {
                    dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                    dt += w(i, X, S, 0.0f) * dit;
                    ut += w(i, X, S, 0.0f) * S[i].Center.X * (1.0f - dit);
                    vt += w(i, X, S, 0.0f) * S[i].Center.Y * (1.0f - dit);
                    zt += w(i, X, S, 0.0f) * t * S[i].Center.Z;
                }
            }
            if (MeanValue.IsChecked == true)
            {
                for (int i = 0; i < S.Count; i++)
                {
                    dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                    dt += gama(i, u, v) * dit;
                    ut += gama(i, u, v) * S[i].Center.X * (1.0f - dit);
                    vt += gama(i, u, v) * S[i].Center.Y * (1.0f - dit);
                    zt += gama(i, u, v) * t * S[i].Center.Z;
                }
            }

            row0[0] = dt;
            row0[3] = ut;

            row1[1] = dt;
            row1[3] = vt;

            row2[2] = dt;
            row2[3] = zt;

            return new Matrix4(row0, row1, row2, row3);
        }

        private Vector3 S(int i, Vector3 X, float t, List<Sphere> S)
        {
            return (M(i, t, S) * new Vector4(X, 1.0f)).Xyz; //(1 - t) * S(i, 1, X) + t * S(i, Spheres[i].R / rRef, X);
        }

        private Vector3 SUp(float u, float v, float t, List<Sphere> S, bool withTranslation)
        {
            if (withTranslation)
            {
                return (K(u, v, t, S) * new Vector4(u, v, Pref.Z, 1.0f)).Xyz;
            }

            return (M(u, v, t, S) * new Vector4(u, v, Pref.Z, 1.0f)).Xyz; //(M(u, v, t, S) * new Vector4(P(new Vector2(u, v), Pref), 1.0f)).Xyz;
        }

        private Vector3 SDown(float u, float v, float t, List<Sphere> S, bool withTranslation)
        {
            if (withTranslation)
            {
                return (K(u, v, t, S) * new Vector4(u, v, -Pref.Z, 1.0f)).Xyz;
            }

            return (M(u, v, t, S) * new Vector4(u, v, -Pref.Z, 1.0f)).Xyz; //(M(u, v, t, S) * new Vector4(P(new Vector2(u, v), Pref), 1.0f)).Xyz;
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
                ds[2] += dwi * (rRef * dit + t * S[i].Center.Z);
                SumWD += w(i, X, S, 0.0f) * dit;
            }

            if (uv == "u") return ds + SumWD * e1;
            else if (uv == "v") return ds + SumWD * e2;
            else return Vector3.Zero;
        }

        // parcialne derivacie spodnej casti
        private Vector3 dSDown(string uv, float u, float v, float t, List<Sphere> S)
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
                ds[2] += dwi * (-rRef * dit + t * S[i].Center.Z);
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

        // normalovy vektor spodnej casti
        private Vector3 normalSD(float u, float v, float t, List<Sphere> S)
        {
            return Vector3.Cross(dSDown("u", u, v, t, S), dSDown("v", u, v, t, S));
        }

        // vypocet derivacii k-tej hornej hranicnej krivky
        private Vector3 dsup(int k, float u, float t, List<Sphere> S)
        {
            Vector3 X, s;
            List<Vector3> n0 = CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
                s = dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
            }
            else
            {
                X = HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
                s = dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
            }

            /*if (k < Spheres.Count - 1) s = Spheres[k + 1].Center - Spheres[k].Center;
            else s = Spheres[0].Center - Spheres[k].Center;*/

            //X = Spheres[k].Center + u * s;
            return dSUp("u", X.X, X.Y, t, S) * s[0] + dSUp("v", X.X, X.Y, t, S) * s[1];
        }

        // vypocet derivacii k-tej spodnej hranicnej krivky
        private Vector3 dsdown(int k, float u, float t, List<Sphere> S)
        {
            Vector3 X, s;
            List<Vector3> n0 = CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
                s = dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
            }
            else
            {
                X = HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
                s = dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
            }

            /*if (k < Spheres.Count - 1) s = Spheres[k + 1].Center - Spheres[k].Center;
            else s = Spheres[0].Center - Spheres[k].Center;*/

            //X = Spheres[k].Center + u * s;
            return dSDown("u", X.X, X.Y, t, S) * s[0] + dSDown("v", X.X, X.Y, t, S) * s[1];
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
        private Vector3 c0(int i, float u, float t, List<Sphere> S, bool withTranslation)
        {
            Vector3 X;
            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[i + 1].CenterInTime(0)); //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
            else X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0)); //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
            return SDown(X.X, X.Y, t, S, withTranslation);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka c1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 c1(int i, float u, float t, List<Sphere> S, bool withTranslation)
        {
            Vector3 X;
            List<Vector3> n0 = CheckKonvexAt(i, 0, S);
            List<Vector3> n1 = CheckKonvexAt(i, 1, S);

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[i + 1].CenterInTime(0)); //X = Spheres[i].Center + u * (Spheres[i + 1].Center - Spheres[i].Center);
            else X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0)); //X = Spheres[i].Center + u * (Spheres[0].Center - Spheres[i].Center);
            return SUp(X.X, X.Y, t, S, withTranslation);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d0 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 d0(int i, float v, float t, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].CenterInTime(0) - S[i].CenterInTime(0);
            else ti3 = S[0].CenterInTime(0) - S[i].CenterInTime(0);
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
            Vector3 centerT = new Vector3(S[i].Center.X, S[i].Center.Y, t * S[i].Center.Z);
            return centerT + S[i].R * dit * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - S[i].R * dit * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;
        }

        // derivacia hranicnej krivky d0 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 dd0(int i, float v, float t, List<Sphere> S)
        {
            //Vector3 X = Spheres[i].Center + new Vector3(0.0f, 0.0f, -Spheres[i].R);
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Vector3 ti3;
            Vector2 s, n;

            if (i < S.Count - 1) ti3 = S[i + 1].CenterInTime(0) - S[i].CenterInTime(0);
            else ti3 = S[0].CenterInTime(0) - S[i].CenterInTime(0);
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

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Id, S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Id, S[0].Center, S[0].R);
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
            Vector3 centerT = new Vector3(C1.Center.X, C1.Center.Y, t * C1.Center.Z);
            return centerT + C1.R * dit * (float)Math.Cos(Math.PI * ((double)v + 1)) * e3 - C1.R * dit * (float)Math.Sin(Math.PI * ((double)v + 1)) * ti2;
        }

        // derivacia hranicnej krivky d1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 dd1(int i, float v, float t, List<Sphere> S)
        {
            Vector3 e3 = new Vector3(0.0f, 0.0f, 1.0f);
            Sphere C1;
            Vector2 s, n;
            Vector3 ti3;

            if (i < S.Count - 1) C1 = new Sphere(S[i + 1].Id, S[i + 1].Center, S[i + 1].R);
            else C1 = new Sphere(S[0].Id, S[0].Center, S[0].R);
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

            if (i < S.Count - 1) X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[i + 1].CenterInTime(0));
            else X = HermiteCurve(u, S[i].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));

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

            if (j == 0)
            {
                nS = normalSD(X.X, X.Y, t, S);
                dcij = dsdown(i, u, t, S);
            }
            else
            {
                nS = normalSH(X.X, X.Y, t, S);
                dcij = dsup(i, u, t, S);
            }

            return Tau * Vector3.Cross(nS, dcij).Normalized();
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // prva tvoriaca zaplata v case t
        private Vector3 Sc(int i, float u, float v, float t, List<Sphere> S, bool withTranslation)
        {
            return H3(0, v) * c0(i, u, t, S, withTranslation) + H3(1, v) * e(i, 0, u, t, S) + H3(2, v) * e(i, 1, u, t, S) + H3(3, v) * c1(i, u, t, S, withTranslation);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // druha tvoriaca zaplata v case t
        private Vector3 Sd(int i, float u, float v, float t, List<Sphere> S)
        {
            return H3(0, u) * d0(i, v, t, S) + H3(1, u) * f(i, 0, v, S) + H3(2, u) * f(i, 1, v, S) + H3(3, u) * d1(i, v, t, S);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // korekcna zaplata v case t
        private Vector3 Scd(int i, float u, float v, float t, List<Sphere> S, bool withTranslation)
        {
            Vector4 Hu = new Vector4(H3(0, u), H3(1, u), H3(2, u), H3(3, u));
            Vector4 Hv = new Vector4(H3(0, v), H3(1, v), H3(2, v), H3(3, v));
            /*Vector3 t00 = Vector3.Zero;
            Vector3 t10 = Vector3.Zero;
            Vector3 t01 = Vector3.Zero;
            Vector3 t11 = Vector3.Zero;*/

            return Hv[0] * (Hu[0] * c0(i, 0, t, S, withTranslation) + Hu[1] * f(i, 0, 0, S) + Hu[2] * f(i, 1, 0, S) + Hu[3] * c0(i, 1, t, S, withTranslation)) +
                   Hv[1] * (Hu[0] * e(i, 0, 0, t, S) /*+ Hu[1] * t00 + Hu[2] * t01*/ + Hu[3] * e(i, 0, 1, t, S)) +
                   Hv[2] * (Hu[0] * e(i, 1, 0, t, S) /*+ Hu[1] * t10 + Hu[2] * t11*/ + Hu[3] * e(i, 1, 1, t, S)) +
                   Hv[3] * (Hu[0] * c1(i, 0, t, S, withTranslation) + Hu[1] * f(i, 0, 1, S) + Hu[2] * f(i, 1, 1, S) + Hu[3] * c1(i, 1, t, S, withTranslation));
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vypocita bod na Coonsovej zaplate na i-tom segmente v case t
        private Vector3 CoonsPatchPoint(int i, float u, float v, float t, List<Sphere> S, bool withTranslation)
        {
            return Sc(i, u, v, t, S, withTranslation) + Sd(i, u, v, t, S) - Scd(i, u, v, t, S, withTranslation);
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
            Vector3[,] Pts = new Vector3[n, lod];
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
                    for (int j = 1; j < n - 1; j++)
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
        private void ComputeUpDownSurface(List<Sphere> S, Vector3 T, int lod, List<Vector3[,,]> samplesUp, List<Vector3[,,]> samplesDown)
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
                    Vector3 C0 = new Vector3(S[i].Center.X, S[i].Center.Y, 0.0f);
                    Vector3 C1;
                    if (i != S.Count - 1) C1 = new Vector3(S[i + 1].Center.X, S[i + 1].Center.Y, 0.0f);
                    else C1 = new Vector3(S[0].Center.X, S[0].Center.Y, 0.0f);
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
                                samplesUp[i][l, m, n] = SUp(t.X, t.Y, casT, S, NonCoplanarSpheres);
                                samplesDown[i][l, m, n] = SDown(t.X, t.Y, casT, S, NonCoplanarSpheres);
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
                                samplesUp[i][l, m, n] = SUp(X.X, X.Y, casT, S, NonCoplanarSpheres);
                                samplesDown[i][l, m, n] = SDown(X.X, X.Y, casT, S, NonCoplanarSpheres);
                            }
                        }

                        samplesUp[i][lod, 0, 0] = SUp(C0.X, C0.Y, casT, S, NonCoplanarSpheres);
                        samplesDown[i][lod, 0, 0] = SDown(C0.X, C0.Y, casT, S, NonCoplanarSpheres);

                        t0 = (float)(lod - 1.0f) / lod;
                        t1 = 1.0f / lod;
                        t = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                        samplesUp[i][lod - 1, 1, 0] = SUp(t.X, t.Y, casT, S, NonCoplanarSpheres);
                        samplesDown[i][lod - 1, 1, 0] = SDown(t.X, t.Y, casT, S, NonCoplanarSpheres);

                        t0 = (float)(lod - 1.0f) / lod;
                        t2 = 1.0f / lod;
                        t = C0 + (1.0f - t0) * (T - C0);
                        samplesUp[i][lod - 1, 0, 1] = SUp(t.X, t.Y, casT, S, NonCoplanarSpheres);
                        samplesDown[i][lod - 1, 0, 1] = SDown(t.X, t.Y, casT, S, NonCoplanarSpheres);
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
                                samplesUp[i][l, m, n] = SurfacePoint(t.Xy, S, true);
                                samplesDown[i][l, m, n] = SurfacePoint(t.Xy, S, false);
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
                                samplesUp[i][l, m, n] = SurfacePoint(X.Xy, S, true);
                                samplesDown[i][l, m, n] = SurfacePoint(X.Xy, S, false);
                            }
                        }

                        samplesUp[i][lod, 0, 0] = SurfacePoint(C0.Xy, S, true);
                        samplesDown[i][lod, 0, 0] = SurfacePoint(C0.Xy, S, false);

                        t0 = (float)(lod - 1.0f) / lod;
                        t1 = 1.0f / lod;
                        t = HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                        samplesUp[i][lod - 1, 1, 0] = SurfacePoint(t.Xy, S, true);
                        samplesDown[i][lod - 1, 1, 0] = SurfacePoint(t.Xy, S, false);

                        t0 = (float)(lod - 1.0f) / lod;
                        t2 = 1.0f / lod;
                        t = C0 + (1.0f - t0) * (T - C0);
                        samplesUp[i][lod - 1, 0, 1] = SurfacePoint(t.Xy, S, true);
                        samplesDown[i][lod - 1, 0, 1] = SurfacePoint(t.Xy, S, false);
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

                            Vector3 normal = Vector3.Cross(SamplesUp[i][l - 1, m + 1, n] - SamplesUp[i][l, m, n], SamplesUp[i][l - 1, m, n + 1] - SamplesUp[i][l - 1, m + 1, n]).Normalized();

                            GL.Begin(PrimitiveType.Triangles);
                            GL.Normal3(normal);
                            GL.Vertex3(SamplesUp[i][l, m, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(SamplesUp[i][l, m, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                            Vector3 normal = Vector3.Cross(SamplesDown[i][l - 1, m, n + 1] - SamplesDown[i][l - 1, m + 1, n], SamplesDown[i][l - 1, m + 1, n] - SamplesDown[i][l, m, n]).Normalized();

                            GL.Begin(PrimitiveType.Triangles);
                            GL.Normal3(normal);
                            GL.Vertex3(SamplesDown[i][l, m, n]);
                            GL.Vertex3(SamplesDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(SamplesDown[i][l, m, n]);
                            GL.Vertex3(SamplesDown[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (m > 0)
                        {
                            if (checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesUp[i][l - 1, m, n + 1] - SamplesUp[i][l, m - 1, n + 1], SamplesUp[i][l, m - 1, n + 1] - SamplesUp[i][l, m, n]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesUp[i][l, m, n]);
                                GL.Vertex3(SamplesUp[i][l, m - 1, n + 1]);
                                GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                                GL.End();
                            }

                            if (checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesDown[i][l, m - 1, n + 1] - SamplesDown[i][l, m, n], SamplesDown[i][l - 1, m, n + 1] - SamplesDown[i][l, m - 1, n + 1]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesDown[i][l, m, n]);
                                GL.Vertex3(SamplesDown[i][l, m - 1, n + 1]);
                                GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                                GL.End();
                            }
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
                            Samples[i][k, j] = CoonsPatchPoint(i, u, v, casT, S, NonCoplanarSpheres);
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
        private void ComputeSkinningSurface()
        {
            if (IsDemo)
            {
                // TODO
                if (Homotopy) casHom = 0.0f;
                else casRozv = 0.0f;

                Sphere sphereRef = _GetRefSphere(Spheres);
                float totalTime = 0.0f;

                foreach (KeyValuePair<int, BranchedSurface> pair in BranchedSurfacesBySideId)
                {
                    BranchedSurface branchedSurface = pair.Value;
                    branchedSurface.rRef = sphereRef.R;
                    branchedSurface.ComputeDownSurface = false;
                    totalTime += branchedSurface.ComputeSkinningSurface(Lod2);
                }

                foreach (KeyValuePair<Tuple<int, int>, SideSurface> pair in SideSurfacesByBranchedPairs)
                {
                    SideSurface sideSurface = pair.Value;
                    totalTime += sideSurface.ComputeSurface(Lod1);
                }

                if (Homotopy) casHom = totalTime;
                else casRozv = totalTime;

                textBox1.Text = "Čas výpočtu 1. metódou: " + (casRozv / 1000.0f).ToString() + " s" + "\n" + "Čas výpočtu 2. metódou: " + (casHom / 1000.0f).ToString() + " s";

                if (Homotopy) casHom = 0.0f;
                else casRozv = 0.0f;
            }
            else
            {
                ComputeSkinningSurface(Spheres, Lod1, Lod2, SamplesUp, SamplesDown);
            }
        }

        private void ComputeSkinningSurface(List<Sphere> S, int lod1, int lod2, List<Vector3[,,]> samplesUp, List<Vector3[,,]> samplesDown)
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
                        S[i].R0 = rRef;
                    }
                }
                else
                {
                    for (int i = 0; i < S.Count; i++)
                    {
                        S[i].R0 = S[i].R;
                    }
                }

                if (Homotopy) casHom = 0.0f;
                else casRozv = 0.0f;

                ComputeUpDownSurface(S, T, lod2, samplesUp, samplesDown);
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
        private void DrawSkinningSurface(List<Sphere> S, int lod1, int lod2)
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
            if (BielaFarba.IsChecked == true) GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
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
            label10.Content = "reper v c1(" + Convert.ToString((float)slider6.Value / 100.0f) + ")";
            label11.Content = "reper v dj(" + Convert.ToString((float)slider4.Value / 100.0f) + ")";
            label14.Content = (int)slider7.Value;
            label15.Content = (int)slider8.Value;
            label18.Content = alpha;
            SpheresSamples.Content = (int)SphereSample.Value;

            // nakreslime potahovu plochu podla zvolenej konstrukcie
            if (ShowSurface && Spheres.Count > 1 && !CreatingSphere && ActiveSphere == -1)
            {
                if (IsDemo)
                {
                    // TODO
                    foreach (KeyValuePair<int, BranchedSurface> pair in BranchedSurfacesBySideId)
                    {
                        BranchedSurface branchedSurface = pair.Value;
                        branchedSurface.DrawSkinningSurface(Lod1, Lod2);
                    }
                    if (checkBox5.IsChecked == true)
                    {
                        foreach (KeyValuePair<Tuple<int, int>, SideSurface> pair in SideSurfacesByBranchedPairs)
                        {
                            SideSurface sideSurface = pair.Value;
                            sideSurface.DrawSurface(Lod1);
                        }
                    }
                }
                else
                {
                    DrawSkinningSurface(Spheres, Lod1, Lod2);
                }
            }
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

        private void _AddSamples(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Samples.Add(new Vector3[120, 120]);
                SamplesUp.Add(new Vector3[41, 41, 41]);
                SamplesDown.Add(new Vector3[41, 41, 41]);
            }
        }

        // zobrazi info o sfere nad ktorou sa prave nachadza mys
        private void ShowInfoAboutPoint(Vector2 mouse)
        {
            Vector3 H = FindIntersection(Spheres, new Vector2(mouse.X, mouse.Y));
            if (HitIndex != -1)
            {
                if (ActiveSphere == -1) Cursor = System.Windows.Input.Cursors.Hand;

                if ((Keyboard.IsKeyDown(Key.M) && IsDemo == false) || Keyboard.IsKeyDown(Key.S) || Keyboard.IsKeyDown(Key.D)) Cursor = System.Windows.Input.Cursors.SizeAll;
                else if (Keyboard.IsKeyDown(Key.Add) || Keyboard.IsKeyDown(Key.Subtract)) Cursor = System.Windows.Input.Cursors.SizeNS;
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
                    if (Keyboard.IsKeyDown(Key.M) && IsDemo == false)
                    {
                        Cursor = System.Windows.Input.Cursors.SizeAll;
                        Spheres[ActiveSphere].Center = center;
                    }
                    else if (Keyboard.IsKeyDown(Key.Add) || Keyboard.IsKeyDown(Key.Subtract)) Cursor = System.Windows.Input.Cursors.SizeNS;
                    else if (Keyboard.IsKeyDown(Key.S) == false && Keyboard.IsKeyDown(Key.D) == false && IsDemo == false) Spheres[ActiveSphere].R = (Spheres[ActiveSphere].Center - HitPoint).Length;

                    CreatingSphere = false;
                }
                else if (Spheres.Count > 1 && ShowTubularNormals.IsChecked == true)
                {
                    HitPoint = FindIntersection(Spheres, Normals, 0.05f, new Vector2(e.X, e.Y));
                    if (HitNormal != -1)
                    {
                        CreatingSphere = false;
                        HitIndex = -1;
                        ActiveSphere = -1;
                    }
                }

                float[] diffuse = { 0.0f, 0.0f, 1.0f, 1.0f };

                if (CreatingSphere && IsDemo == false)
                {
                    Spheres.Add(new Sphere(Spheres.Count, center, 0.1f, diffuse));
                    ComputeTubularNormals();
                    _AddSamples();
                    RightX = e.X;
                    RightY = e.Y;
                }
                if (checkBox7.IsChecked == true) ComputeSkinningSurface();
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
            else if (ActiveSphere != -1)
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

                if (Keyboard.IsKeyDown(Key.M) && IsDemo == false)
                {
                    Cursor = System.Windows.Input.Cursors.SizeAll;
                    Spheres[ActiveSphere].Center += P - Spheres[ActiveSphere].Center;
                }
                else if (Keyboard.IsKeyDown(Key.Add) || Keyboard.IsKeyDown(Key.Subtract)) Cursor = System.Windows.Input.Cursors.SizeNS;
                else if (Keyboard.IsKeyDown(Key.S) == false && Keyboard.IsKeyDown(Key.D) == false && IsDemo == false) Spheres[ActiveSphere].R = (P - Spheres[ActiveSphere].Center).Length;

                if (checkBox7.IsChecked == true && tubularna.IsChecked == true) ComputeTubularSurface(Lod1);

                RightY = e.Y;
                RightX = e.X;
            }
            else if (HitNormal != -1)
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
                if (checkBox7.IsChecked == true) ComputeSkinningSurface();
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
                    SamplesUp.RemoveAt(HitIndex);
                    SamplesDown.RemoveAt(HitIndex);
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

        private void GLControl_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (ActiveSphere != -1)
            {
                Vector3 centerDirection = Spheres[ActiveSphere].Center.Normalized();
                if (e.KeyCode == Keys.Add)
                {

                    Spheres[ActiveSphere].Center += IsDemo ? step * centerDirection : step * e3;

                    // prekresli scenu
                    glControl.Invalidate();
                }
                else if (e.KeyCode == Keys.Subtract)
                {
                    Spheres[ActiveSphere].Center -= IsDemo ? step * centerDirection : step * e3;

                    // prekresli scenu
                    glControl.Invalidate();
                }
                else if (e.KeyCode == Keys.S && IsDemo)
                {
                    Spheres[ActiveSphere].R += step;

                    // prekresli scenu
                    glControl.Invalidate();
                }
                else if (e.KeyCode == Keys.D && IsDemo)
                {
                    Spheres[ActiveSphere].R -= step;

                    // prekresli scenu
                    glControl.Invalidate();
                }
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
            if (ShowSurface) ComputeSkinningSurface();
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

            if (ShowSurface) ComputeSkinningSurface();
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
            if (ShowSurface) ComputeSkinningSurface();

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
                ComputeUpDownSurface(Spheres, T, Lod2, SamplesUp, SamplesDown);
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
            if (ShowSurface) ComputeSkinningSurface();

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
            if (ShowSurface) ComputeSkinningSurface();

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
            if (ShowSurface && RozvetvenaHomotopia.IsChecked == true) ComputeSkinningSurface();

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

            ComputeSkinningSurface();

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
            _Clear();
            IsRightDown = false;
            IsDemo = false;
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
        private Vector3 _centerTemp;
        private Vector3 _centerOriginalTemp;

        public int Id { get; set; }
        public float R { get; set; }
        public float R0 { get; set; }
        public Vector3 Center { get; set; }
        public Vector3 CenterOriginal { get; set; }
        public float[] DiffColor { get; set; }

        public Vector3 Translation => new Vector3(Center - CenterOriginal);

        public Sphere(int id, Vector3 center, float r, float[] diffColor, Vector3 centerOriginal)
        {
            Id = id;
            Center = new Vector3(center);
            CenterOriginal = new Vector3(centerOriginal);
            R = r;
            R0 = R;
            DiffColor = diffColor;
        }

        public Sphere(int id, Vector3 center, float r, float[] diffColor)
        {
            Id = id;
            Center = new Vector3(center);
            CenterOriginal = new Vector3(center);
            R = r;
            R0 = R;
            DiffColor = diffColor;
        }

        // ak nezvolime farbu tak defaultne nastavime na cervenu
        public Sphere(int id, Vector3 center, float r)
        {
            Id = id;
            Center = new Vector3(center);
            CenterOriginal = new Vector3(center);
            R = r;
            R0 = R;
            DiffColor = new float[] { 1.0f, 0.0f, 0.0f, 1.0f };
        }

        public Sphere(int id, Vector3 center, float r, Vector3 centerOriginal)
        {
            Id = id;
            Center = new Vector3(center);
            CenterOriginal = new Vector3(centerOriginal);
            R = r;
            R0 = R;
            DiffColor = new float[] { 1.0f, 0.0f, 0.0f, 1.0f };
        }

        public Vector3 CenterInTime(float time)
        {
            return new Vector3(CenterOriginal + time * Translation);
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
                    Vector3 C = Center;
                    if (MainWindow.Homotopy)
                    {
                        /*Matrix4 N = M(MainWindow.casT);
                        P1 = (N * new Vector4(P1, 1.0f)).Xyz;
                        P2 = (N * new Vector4(P2, 1.0f)).Xyz;
                        P3 = (N * new Vector4(P3, 1.0f)).Xyz;
                        P4 = (N * new Vector4(P4, 1.0f)).Xyz;*/
                        float dt = 1.0f + (1.0f - MainWindow.casT) * (R0 / R - 1.0f);
                        if (MainWindow.NonCoplanarSpheres)
                        {
                            C = CenterInTime(MainWindow.casT);
                        }

                        P1 = new Vector3(C.X - R * dt * (float)Math.Cos(theta) * (float)Math.Cos(phi), C.Y - R * dt * (float)Math.Cos(theta) * (float)Math.Sin(phi), C.Z - R * dt * (float)Math.Sin(theta));
                        P2 = new Vector3(C.X - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi), C.Y - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi), C.Z - R * dt * (float)Math.Sin(theta + h1));
                        P3 = new Vector3(C.X - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi + h2), C.Y - R * dt * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi + h2), C.Z - R * dt * (float)Math.Sin(theta + h1));
                        P4 = new Vector3(C.X - R * dt * (float)Math.Cos(theta) * (float)Math.Cos(phi + h2), C.Y - R * dt * (float)Math.Cos(theta) * (float)Math.Sin(phi + h2), C.Z - R * dt * (float)Math.Sin(theta));
                    }
                    else
                    {
                        P1 = new Vector3(C.X - R * (float)Math.Cos(theta) * (float)Math.Cos(phi), C.Y - R * (float)Math.Cos(theta) * (float)Math.Sin(phi), C.Z - R * (float)Math.Sin(theta));
                        P2 = new Vector3(C.X - R * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi), C.Y - R * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi), C.Z - R * (float)Math.Sin(theta + h1));
                        P3 = new Vector3(C.X - R * (float)Math.Cos(theta + h1) * (float)Math.Cos(phi + h2), C.Y - R * (float)Math.Cos(theta + h1) * (float)Math.Sin(phi + h2), C.Z - R * (float)Math.Sin(theta + h1));
                        P4 = new Vector3(C.X - R * (float)Math.Cos(theta) * (float)Math.Cos(phi + h2), C.Y - R * (float)Math.Cos(theta) * (float)Math.Sin(phi + h2), C.Z - R * (float)Math.Sin(theta));
                    }

                    Vector3 n1 = (P1 - C).Normalized();
                    Vector3 n2 = (P2 - C).Normalized();
                    Vector3 n3 = (P3 - C).Normalized();
                    Vector3 n4 = (P4 - C).Normalized();

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

        public void SetLocal(Matrix4 matrix)
        {
            _centerTemp = new Vector3(Center);
            _centerOriginalTemp = new Vector3(CenterOriginal);

            _ApplyMatrix(matrix);
        }

        public void SetGlobal()
        {
            Center = _centerTemp;
            CenterOriginal = _centerOriginalTemp;
        }

        private void _ApplyMatrix(Matrix4 matrix)
        {
            Vector4 c = matrix * new Vector4(Center.X, Center.Y, Center.Z, 1.0f);
            Vector4 cOrig = matrix * new Vector4(CenterOriginal.X, CenterOriginal.Y, CenterOriginal.Z, 1.0f);
            Center = new Vector3(c.Xyz / c.W);
            CenterOriginal = new Vector3(cOrig.Xyz / cOrig.W);
        }
    }

    class BranchedSurface
    {
        private readonly MainWindow _window;

        private bool _areSpheresInGlobal = true;

        public int Id { get; set; } = 0;
        public List<Sphere> Spheres { get; set; } = new List<Sphere>();
        public List<Vector3[,,]> SamplesUp { get; set; } = new List<Vector3[,,]>();
        public List<Vector3[,,]> SamplesDown { get; set; } = new List<Vector3[,,]>();
        public List<Vector3[,]> Samples { get; set; } = new List<Vector3[,]>();

        public Matrix4 Matrix = new Matrix4();
        public Matrix4 InverseMatrix = new Matrix4();

        // referencny polomer
        public float rRef { get; set; }
        public bool ComputeDownSurface { get; set; } = true;

        public BranchedSurface(MainWindow window, int id, List<Sphere> spheres, Matrix4 matrix)
        {
            _window = window;
            Id = id;
            Spheres = spheres;
            Matrix = matrix;
            InverseMatrix = Matrix.Inverted();
            _AddSamples(Spheres?.Count ?? 0);
            _SetRefRadius(Spheres);
        }

        public BranchedSurface(MainWindow window, int id, List<Sphere> spheres, Sphere sphereRef, Matrix4 matrix)
        {
            _window = window;
            Id = id;
            Spheres = spheres;
            Matrix = matrix;
            InverseMatrix = Matrix.Inverted();
            _AddSamples(Spheres?.Count ?? 0);
            _SetRefRadius(sphereRef);
        }

        public long ComputeSkinningSurface(int lod)
        {
            long totalTime = 0;
            // ak je zvolena rozvetvena konstrukcia tak pocitame hornu/dolnu a bocnu cast
            if (_window.rozvetvena.IsChecked == true || _window.RozvetvenaHomotopia.IsChecked == true)
            {
                if (_window.RozvetvenaHomotopia.IsChecked == true)
                {
                    for (int i = 0; i < Spheres.Count; i++)
                    {
                        Spheres[i].R0 = rRef;
                    }
                }
                else
                {
                    for (int i = 0; i < Spheres.Count; i++)
                    {
                        Spheres[i].R0 = Spheres[i].R;
                    }
                }

                totalTime += _ComputeUpDownSurface(lod);
                // totalTime +=_ComputeSideSurface(S, lod1);
            }

            return totalTime;
        }

        // vypocet bodov hornej a dolnej casti potahovej plochy
        private long _ComputeUpDownSurface(int lod)
        {
            TransformSpheresToLocal();

            List<Sphere> S = Spheres;

            Stopwatch stopWatch = new Stopwatch();

            Vector3 T = new Vector3(0.0f, 0.0f, 0.0f);
            for (int i = 0; i < S.Count; i++)
            {
                T += S[i].CenterInTime(0) / S.Count;
            }

            T.Z = 0.0f;

            int n;
            float t0, t1, t2;
            Vector3 t;

            stopWatch.Start();
            for (int i = 0; i < S.Count; i++)
            {
                Vector3 C0 = new Vector3(S[i].CenterInTime(0).X, S[i].CenterInTime(0).Y, 0.0f);
                Vector3 C1;
                if (i != S.Count - 1) C1 = new Vector3(S[i + 1].CenterInTime(0).X, S[i + 1].CenterInTime(0).Y, 0.0f);
                else C1 = new Vector3(S[0].CenterInTime(0).X, S[0].CenterInTime(0).Y, 0.0f);
                Vector3 X;
                List<Vector3> n0 = _CheckKonvexAt(i, 0, S);
                List<Vector3> n1 = _CheckKonvexAt(i, 1, S);

                if (MainWindow.det(n0[1].Xy, n1[0].Xy) < MainWindow.eps && MainWindow.det(n0[1].Xy, n1[0].Xy) > -MainWindow.eps)
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
                            SamplesUp[i][l, m, n] = _SUp(t.X, t.Y, MainWindow.casT, S);
                            if (ComputeDownSurface)
                            {
                                SamplesDown[i][l, m, n] = _SDown(t.X, t.Y, MainWindow.casT, S);
                            }
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
                            Vector3 h = MainWindow.HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                            X = c0 * t2 / (t1 + t2) + h * t1 / (t1 + t2);

                            SamplesUp[i][l, m, n] = _SUp(X.X, X.Y, MainWindow.casT, S);

                            if (ComputeDownSurface)
                            {
                                SamplesDown[i][l, m, n] = _SDown(X.X, X.Y, MainWindow.casT, S);
                            }
                        }
                    }

                    SamplesUp[i][lod, 0, 0] = _SUp(C0.X, C0.Y, MainWindow.casT, S);
                    if (ComputeDownSurface)
                    {
                        SamplesDown[i][lod, 0, 0] = _SDown(C0.X, C0.Y, MainWindow.casT, S);
                    }

                    t0 = (float)(lod - 1.0f) / lod;
                    t1 = 1.0f / lod;
                    t = MainWindow.HermiteCurve(1.0f - t0, C0, n0[1], n1[0], C1);
                    SamplesUp[i][lod - 1, 1, 0] = _SUp(t.X, t.Y, MainWindow.casT, S);
                    if (ComputeDownSurface)
                    {
                        SamplesDown[i][lod - 1, 1, 0] = _SDown(t.X, t.Y, MainWindow.casT, S);
                    }

                    t0 = (float)(lod - 1.0f) / lod;
                    t2 = 1.0f / lod;
                    t = C0 + (1.0f - t0) * (T - C0);
                    SamplesUp[i][lod - 1, 0, 1] = _SUp(t.X, t.Y, MainWindow.casT, S);
                    if (ComputeDownSurface)
                    {
                        SamplesDown[i][lod - 1, 0, 1] = _SDown(t.X, t.Y, MainWindow.casT, S);
                    }
                }

            }

            TransformSpheresToGlobal();

            stopWatch.Stop();

            return stopWatch.ElapsedMilliseconds;
        }


        // vykreslovanie celej potahovej plochy
        public void DrawSkinningSurface(int lod1, int lod2)
        {
            // ak je zvolena rozvetvena konstrukcia tak kreslime hornu/dolnu a bocnu cast
            if (_window.rozvetvena.IsChecked == true || _window.RozvetvenaHomotopia.IsChecked == true)
            {
                if (_window.checkBox6.IsChecked == true) _DrawUpDownSurface(Spheres, lod2);
                // if (m_window.checkBox5.IsChecked == true) DrawSideSurface(S, lod1);
            }
            // ak je zvolena tubularna konstrukcia tak kreslime body tubularnej potahovej plochy
            if (_window.tubularna.IsChecked == true)
            {
                // DrawTubularSurface(lod1);
            }
        }

        // vykreslenie hornej a dolnej casti potahovej plochy
        private void _DrawUpDownSurface(List<Sphere> S, int lod)
        {
            float[] red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float[] Diffuse, Ambient, Specular = { 0.5f, 0.5f, 0.5f, 1.0f };
            float Shininess = 0.5f;
            if (MainWindow.ColorOne)
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
                // vykreslenie hornej/dolnej casti plochy
                for (int l = lod; l > 0; l--)
                {
                    for (int m = lod - l; m >= 0; m--)
                    {
                        int n = lod - (l + m);
                        if (_window.checkBox4.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                            Vector3 normal = Vector3.Cross(SamplesUp[i][l - 1, m + 1, n] - SamplesUp[i][l, m, n], SamplesUp[i][l - 1, m, n + 1] - SamplesUp[i][l - 1, m + 1, n]).Normalized();

                            GL.Begin(PrimitiveType.Triangles);
                            GL.Normal3(normal);
                            GL.Vertex3(SamplesUp[i][l, m, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (_window.checkBox3.IsChecked == true)
                        {
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                            GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                            GL.LineWidth(1.0f);
                            GL.Begin(PrimitiveType.LineLoop);
                            GL.Vertex3(SamplesUp[i][l, m, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m + 1, n]);
                            GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                            GL.End();
                        }

                        if (ComputeDownSurface)
                        {
                            if (_window.checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesDown[i][l - 1, m, n + 1] - SamplesDown[i][l - 1, m + 1, n], SamplesDown[i][l - 1, m + 1, n] - SamplesDown[i][l, m, n]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesDown[i][l, m, n]);
                                GL.Vertex3(SamplesDown[i][l - 1, m + 1, n]);
                                GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                                GL.End();
                            }

                            if (_window.checkBox3.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                                GL.LineWidth(1.0f);
                                GL.Begin(PrimitiveType.LineLoop);
                                GL.Vertex3(SamplesDown[i][l, m, n]);
                                GL.Vertex3(SamplesDown[i][l - 1, m + 1, n]);
                                GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                                GL.End();
                            }
                        }

                        if (m > 0)
                        {
                            if (_window.checkBox4.IsChecked == true)
                            {
                                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                Vector3 normal = Vector3.Cross(SamplesUp[i][l - 1, m, n + 1] - SamplesUp[i][l, m - 1, n + 1], SamplesUp[i][l, m - 1, n + 1] - SamplesUp[i][l, m, n]).Normalized();

                                GL.Begin(PrimitiveType.Triangles);
                                GL.Normal3(normal);
                                GL.Vertex3(SamplesUp[i][l, m, n]);
                                GL.Vertex3(SamplesUp[i][l, m - 1, n + 1]);
                                GL.Vertex3(SamplesUp[i][l - 1, m, n + 1]);
                                GL.End();
                            }

                            if (ComputeDownSurface)
                            {
                                if (_window.checkBox4.IsChecked == true)
                                {
                                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                                    GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                                    Vector3 normal = Vector3.Cross(SamplesDown[i][l, m - 1, n + 1] - SamplesDown[i][l, m, n], SamplesDown[i][l - 1, m, n + 1] - SamplesDown[i][l, m - 1, n + 1]).Normalized();

                                    GL.Begin(PrimitiveType.Triangles);
                                    GL.Normal3(normal);
                                    GL.Vertex3(SamplesDown[i][l, m, n]);
                                    GL.Vertex3(SamplesDown[i][l, m - 1, n + 1]);
                                    GL.Vertex3(SamplesDown[i][l - 1, m, n + 1]);
                                    GL.End();
                                }
                            }
                        }
                    }
                }
            }
        }

        public void TransformSpheresToLocal()
        {
            if (_areSpheresInGlobal)
            {
                foreach (Sphere sphere in Spheres)
                {
                    sphere.SetLocal(Matrix);
                }
                _areSpheresInGlobal = false;
            }
        }

        public void TransformSpheresToGlobal()
        {
            if (_areSpheresInGlobal == false)
            {
                foreach (Sphere sphere in Spheres)
                {
                    sphere.SetGlobal();
                }
                _areSpheresInGlobal = true;
            }
        }

        private void _AddSamples(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Samples.Add(new Vector3[120, 120]);
                SamplesUp.Add(new Vector3[41, 41, 41]);
                SamplesDown.Add(new Vector3[41, 41, 41]);
            }
        }

        private Vector3 _SUp(float u, float v, float t, List<Sphere> S)
        {
            return (InverseMatrix * _K(u, v, t, S) * new Vector4(u, v, rRef, 1.0f)).Xyz;
        }

        public Vector3 SUp(float u, float v, float t)
        {
            return _SUp(u, v, t, Spheres);
        }

        private Vector3 _SDown(float u, float v, float t, List<Sphere> S)
        {
            return (InverseMatrix * _K(u, v, t, S) * new Vector4(u, v, -rRef, 1.0f)).Xyz;
        }

        // parcialne derivacie hornej casti
        private Vector3 _dSUp(string uv, float u, float v, float t, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            Vector3 ds = Vector3.Zero;
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);

            for (int i = 0; i < S.Count; i++)
            {
                Vector3 center = S[i].CenterInTime(0);
                if (Vector2.Dot(X - center.Xy, X - center.Xy) <= MainWindow.eps && uv == "u") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e1;
                if (Vector2.Dot(X - center.Xy, X - center.Xy) <= MainWindow.eps && uv == "v") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e2;
            }

            float dit, dwi;
            float SumWD = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                Vector3 center = S[i].CenterInTime(0);
                dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                dwi = _dW(uv, i, u, v, 0.0f, S);
                ds[0] += dwi * (u * dit + center.X * (1.0f - dit) + t * S[i].Translation.X);
                ds[1] += dwi * (v * dit + center.Y * (1.0f - dit) + t * S[i].Translation.Y);
                ds[2] += dwi * (rRef * dit + t * S[i].Translation.Z);
                SumWD += _w(i, X, S, 0.0f) * dit;
            }

            if (uv == "u") return ds + SumWD * e1;
            else if (uv == "v") return ds + SumWD * e2;
            else return Vector3.Zero;
        }

        // parcialne derivacie spodnej casti
        private Vector3 _dSDown(string uv, float u, float v, float t, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            Vector3 ds = Vector3.Zero;
            Vector3 e1 = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 e2 = new Vector3(0.0f, 1.0f, 0.0f);

            for (int i = 0; i < S.Count; i++)
            {
                Vector3 center = S[i].CenterInTime(0);
                if (Vector2.Dot(X - center.Xy, X - center.Xy) <= MainWindow.eps && uv == "u") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e1;
                if (Vector2.Dot(X - center.Xy, X - center.Xy) <= MainWindow.eps && uv == "v") return (1.0f + t * (S[i].R / rRef - 1.0f)) * e2;
            }

            float dit, dwi;
            float SumWD = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                Vector3 center = S[i].CenterInTime(0);
                dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                dwi = _dW(uv, i, u, v, 0.0f, S);
                ds[0] += dwi * (u * dit + center.X * (1.0f - dit) + t * S[i].Translation.X);
                ds[1] += dwi * (v * dit + center.Y * (1.0f - dit) + t * S[i].Translation.Y);
                ds[2] += dwi * (-rRef * dit + t * S[i].Translation.Z);
                SumWD += _w(i, X, S, 0.0f) * dit;
            }

            if (uv == "u") return ds + SumWD * e1;
            else if (uv == "v") return ds + SumWD * e2;
            else return Vector3.Zero;
        }

        // normalovy vektor hornej casti
        private Vector3 _normalSH(float u, float v, float t, List<Sphere> S)
        {
            return Vector3.Cross(_dSUp("u", u, v, t, S), _dSUp("v", u, v, t, S));
        }

        public Vector3 normalSH(float u, float v, float t)
        {
            return _normalSH(u, v, t, Spheres);
        }

        // normalovy vektor spodnej casti
        private Vector3 _normalSD(float u, float v, float t, List<Sphere> S)
        {
            return Vector3.Cross(_dSDown("u", u, v, t, S), _dSDown("v", u, v, t, S));
        }

        // vypocet derivacii k-tej hornej hranicnej krivky
        private Vector3 _dsup(int k, float u, float t, List<Sphere> S)
        {
            Vector3 X, s;
            List<Vector3> n0 = _CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = _CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = MainWindow.HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
                s = MainWindow.dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
            }
            else
            {
                X = MainWindow.HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
                s = MainWindow.dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
            }

            return _dSUp("u", X.X, X.Y, t, S) * s[0] + _dSUp("v", X.X, X.Y, t, S) * s[1];
        }

        // vypocet derivacii k-tej hornej hranicnej krivky
        public Vector3 dsup(float u, float t, Sphere S1, Sphere S2)
        {
            Vector3 X, s;
            Vector3 center1 = S1.CenterInTime(0);
            Vector3 center2 = S2.CenterInTime(0);
            Vector3 v = center2 - center1;

            X = MainWindow.HermiteCurve(u, center1, v, v, center2);
            s = MainWindow.dHermiteCurve(u, center1, v, v, center2);

            return _dSUp("u", X.X, X.Y, t, Spheres) * s[0] + _dSUp("v", X.X, X.Y, t, Spheres) * s[1];
        }

        public Vector3 dsup(int k, float u, float t)
        {
            return _dsup(k, u, t, Spheres);
        }

        // vypocet derivacii k-tej spodnej hranicnej krivky
        private Vector3 _dsdown(int k, float u, float t, List<Sphere> S)
        {
            Vector3 X, s;
            List<Vector3> n0 = _CheckKonvexAt(k, 0, S);
            List<Vector3> n1 = _CheckKonvexAt(k, 1, S);

            if (k < S.Count - 1)
            {
                X = MainWindow.HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
                s = MainWindow.dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[k + 1].CenterInTime(0));
            }
            else
            {
                X = MainWindow.HermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
                s = MainWindow.dHermiteCurve(u, S[k].CenterInTime(0), n0[1], n1[0], S[0].CenterInTime(0));
            }

            return _dSDown("u", X.X, X.Y, t, S) * s[0] + _dSDown("v", X.X, X.Y, t, S) * s[1];
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private static List<Vector3> _CheckKonvexAt(int i, int j, List<Sphere> S)
        {
            Vector3 t0 = new Vector3(), t1 = new Vector3();
            if (S.Count > 1)
            {
                if (i >= 1 && i <= S.Count - 3)
                {
                    t0 = S[i + j].CenterInTime(0) - S[i + j - 1].CenterInTime(0);
                    t1 = S[i + j + 1].CenterInTime(0) - S[i + j].CenterInTime(0);
                }
                else if (i == 0)
                {
                    if (j == 0)
                    {
                        t0 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                        t1 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                        if (S.Count > 2) t1 = S[2].CenterInTime(0) - S[1].CenterInTime(0);
                        else t1 = S[0].CenterInTime(0) - S[1].CenterInTime(0);
                    }
                }
                else if (i == S.Count - 2)
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 2].CenterInTime(0) - S[S.Count - 3].CenterInTime(0);
                        t1 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                        t1 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                    }
                }
                else
                {
                    if (j == 0)
                    {
                        t0 = S[S.Count - 1].CenterInTime(0) - S[S.Count - 2].CenterInTime(0);
                        t1 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                    }
                    else
                    {
                        t0 = S[0].CenterInTime(0) - S[S.Count - 1].CenterInTime(0);
                        t1 = S[1].CenterInTime(0) - S[0].CenterInTime(0);
                    }
                }

                if (MainWindow.det(t0.Xy, t1.Xy) < -MainWindow.eps)
                {
                    Vector3 s = (t0 + t1) / 2.0f;
                    t0 = s;
                    t1 = s;
                }
            }

            t0.Z = 0.0f;
            t1.Z = 0.0f;

            return new List<Vector3> { t0, t1 };
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // i-ta vahova funkcia v bode X pri danej mnozine sfer S
        private float _w(int i, Vector2 X, List<Sphere> S, float delta)
        {
            for (int j = 0; j < S.Count; j++)
            {
                if ((X - S[j].CenterInTime(0).Xy).Length - delta * S[j].R <= MainWindow.eps) return MainWindow.KDelta(i, j); //if (Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2) <= eps) return KDelta(i, j);
            }

            float wSum = 0.0f;
            for (int j = 0; j < S.Count; j++)
            {
                wSum += _p(j, X, S, delta); //* (float)Math.Pow((P(X, S)- S[j].Center).Length, n));
            }
            return _p(i, X, S, delta) / wSum; //* (float)Math.Pow((P(X, S) - S[i].Center).Length, n))) / wSum;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // funkcia v citateli i-tej vahovej funkcie, ktora sluzi na stabilnejsie vyjadrenie vahovych funkcii
        private float _p(int i, Vector2 X, List<Sphere> S, float delta)
        {
            float pSum = 1.0f;
            for (int j = 0; j < i; j++)
            {
                pSum *= (float)Math.Pow((X - S[j].CenterInTime(0).Xy).Length - delta * S[j].R, MainWindow.Eta); //(float)Math.Pow(Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2), n);
            }
            for (int j = i + 1; j < S.Count; j++)
            {
                pSum *= (float)Math.Pow((X - S[j].CenterInTime(0).Xy).Length - delta * S[j].R, MainWindow.Eta); //(float)Math.Pow(Vector2.Dot(X - S[j].Center.Xy, X - S[j].Center.Xy) - Math.Pow(delta * S[j].R, 2), n);
            }
            return pSum;
        }

        private Matrix4 _K(float u, float v, float t, List<Sphere> S)
        {
            float dt = 0.0f;
            float ut = 0.0f, vt = 0.0f, zt = 0.0f, dit;

            Vector2 X = new Vector2(u, v);

            Vector4 row0 = Vector4.Zero;
            Vector4 row1 = Vector4.Zero;
            Vector4 row2 = Vector4.Zero;
            Vector4 row3 = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

            for (int i = 0; i < S.Count; i++)
            {
                Vector3 center = S[i].CenterInTime(0);
                dit = 1.0f + t * (S[i].R / rRef - 1.0f);
                dt += _w(i, X, S, 0.0f) * dit;
                ut += _w(i, X, S, 0.0f) * (center.X * (1.0f - dit) + t * S[i].Translation.X);
                vt += _w(i, X, S, 0.0f) * (center.Y * (1.0f - dit) + t * S[i].Translation.Y);
                zt += _w(i, X, S, 0.0f) * t * S[i].Translation.Z; // _w(i, X, S, 0.0f) * t * center.Z;
            }

            row0[0] = dt;
            row0[3] = ut;

            row1[1] = dt;
            row1[3] = vt;

            row2[2] = dt;
            row2[3] = zt;

            return new Matrix4(row0, row1, row2, row3);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // parcialna derivacia podla u/v i-tej vahovej funkcie
        private float _dW(string uv, int i, float u, float v, float delta, List<Sphere> S)
        {
            return (_dF(uv, i, u, v, delta, S) * _G(u, v, delta, S) - _F(i, u, v, delta, S) * _dG(uv, u, v, delta, S)) / (float)Math.Pow(_G(u, v, delta, S), 2);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private float _F(int i, float u, float v, float delta, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            return 1.0f / (float)Math.Pow((X - S[i].CenterInTime(0).Xy).Length - delta * S[i].R, MainWindow.Eta);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private float _dF(string uv, int i, float u, float v, float delta, List<Sphere> S)
        {
            Vector2 X = new Vector2(u, v);
            if (uv == "u") return -MainWindow.Eta * (u - S[i].CenterInTime(0).X) / ((X - S[i].CenterInTime(0).Xy).Length * (float)Math.Pow((X - S[i].CenterInTime(0).Xy).Length - delta * S[i].R, MainWindow.Eta + 1));
            else if (uv == "v") return -MainWindow.Eta * (v - S[i].CenterInTime(0).Y) / ((X - S[i].CenterInTime(0).Xy).Length * (float)Math.Pow((X - S[i].CenterInTime(0).Xy).Length - delta * S[i].R, MainWindow.Eta + 1));
            else return 0.0f;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private float _G(float u, float v, float delta, List<Sphere> S)
        {
            float gSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                gSum += _F(i, u, v, delta, S);
            }
            return gSum;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private float _dG(string uv, float u, float v, float delta, List<Sphere> S)
        {
            float gSum = 0.0f;
            for (int i = 0; i < S.Count; i++)
            {
                gSum += _dF(uv, i, u, v, delta, S);
            }
            return gSum;
        }

        // nastavenie referencneho bodu
        private void _SetRefRadius(List<Sphere> S, bool min = false)
        {
            float rValue = min ? float.MaxValue : float.MinValue;

            for (int i = 0; i < S.Count; i++)
            {
                if (min && rValue > S[i].R)
                {
                    rValue = S[i].R;
                }
                else if (rValue < S[i].R)
                {
                    rValue = S[i].R;
                }
            }

            rRef = rValue;
        }

        private void _SetRefRadius(Sphere S)
        {
            rRef = S.R;
        }
    }

    class SideSurface
    {
        private readonly MainWindow _window;
        private readonly BranchedSurface _branchedSurface1;
        private readonly BranchedSurface _branchedSurface2;

        private Sphere Sphere1 { get; set; } = null;
        private Sphere Sphere2 { get; set; } = null;

        public int Id { get; set; } = 0;
        public Vector3[,] Samples { get; set; } = new Vector3[120, 120];

        public SideSurface(MainWindow window, int id, BranchedSurface branchedSurface1, BranchedSurface branchedSurface2)
        {
            _window = window;
            Id = id;
            _branchedSurface1 = branchedSurface1;
            _branchedSurface2 = branchedSurface2;

            _SetCommonSpheres();
            _ValidateCommonSpheres();
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vypocet bodov vsetkych segmentov bocnej potahovej plochy
        public long ComputeSurface(int lod)
        {
            Stopwatch stopWatch = new Stopwatch();
            long time = 0;

            if (Sphere1 == null || Sphere2 == null)
            {
                return time;
            }

            if (MainWindow.Homotopy)
            {
                stopWatch.Start();
                // vypocet vo vzorkovacich bodoch na "okrajoch" hornej casti plochy
                for (int j = 0; j < lod; j++)
                {
                    float u = (float)j / (lod - 1.0f);

                    // vypocitame ich ako body na Coonsovej zaplate
                    for (int k = 0; k < lod; k++)
                    {
                        float v = (float)k / (lod - 1.0f);
                        Samples[k, j] = _CoonsPatchPoint(u, v, MainWindow.casT);
                    }
                }
                stopWatch.Stop();
                time += stopWatch.ElapsedMilliseconds;
            }

            return time;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vykreslenie bodov vsetkych segmentov bocnej potahovej plochy
        public void DrawSurface(int lod)
        {
            float[] red = { 1.0f, 0.0f, 0.0f, 1.0f };
            float[] Diffuse = { 1.0f, 1.0f, 0.0f, 1.0f }, Ambient, Specular = { 0.5f, 0.5f, 0.5f, 1.0f };
            float Shininess = 0.7f;

            if (MainWindow.ColorOne)
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

            // vykreslenie bocnej casti plochy
            for (int j = 0; j < lod - 1; j++)
            {
                for (int k = 0; k < lod - 1; k++)
                {
                    if (_window.checkBox4.IsChecked == true)
                    {
                        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, Diffuse);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, Ambient);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Specular, Specular);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Shininess, Shininess);

                        // vypocet normal vo vrcholoch
                        Vector3 n1 = _ComputeNormal(j, k).Normalized();
                        Vector3 n2 = _ComputeNormal(j + 1, k).Normalized();
                        Vector3 n3 = _ComputeNormal(j + 1, k + 1).Normalized();
                        Vector3 n4 = _ComputeNormal(j, k + 1).Normalized();

                        GL.Begin(PrimitiveType.Quads);
                        GL.Normal3(n1);
                        GL.Vertex3(Samples[j, k]);
                        GL.Normal3(n2);
                        GL.Vertex3(Samples[j + 1, k]);
                        GL.Normal3(n3);
                        GL.Vertex3(Samples[j + 1, k + 1]);
                        GL.Normal3(n4);
                        GL.Vertex3(Samples[j, k + 1]);
                        GL.End();
                    }

                    if (_window.checkBox3.IsChecked == true)
                    {
                        GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, red);
                        GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Ambient, red);
                        GL.LineWidth(1.0f);
                        GL.Begin(PrimitiveType.LineLoop);
                        GL.Vertex3(Samples[j, k]);
                        GL.Vertex3(Samples[j + 1, k]);
                        GL.Vertex3(Samples[j + 1, k + 1]);
                        GL.Vertex3(Samples[j, k + 1]);
                        GL.End();
                    }
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------

        private void _SetCommonSpheres()
        {
            Sphere1 = null;
            Sphere2 = null;

            if (_branchedSurface1 == null || _branchedSurface2 == null)
            {
                return;
            }

            for (int i = 0; i < _branchedSurface1.Spheres.Count; i++)
            {
                Sphere sphere1 = _branchedSurface1.Spheres[i];
                for (int j = 0; j < _branchedSurface2.Spheres.Count; j++)
                {
                    Sphere sphere2 = _branchedSurface2.Spheres[j];
                    if (Sphere1 != null && Sphere2 != null)
                    {
                        return;
                    }

                    if (Sphere1 == null && sphere1 == sphere2)
                    {
                        Sphere1 = sphere1;
                    }
                    else if (Sphere2 == null && sphere1 == sphere2)
                    {
                        Sphere2 = sphere1;
                    }
                }
            }
        }

        private void _ValidateCommonSpheres()
        {
            if (Sphere1 != null && Sphere2 != null)
            {
                Vector3 center1 = Sphere1.CenterInTime(0);
                Vector3 center2 = Sphere2.CenterInTime(0);

                Vector3 B = _c0(0, 0);
                Vector3 A = _c1(0, 0);
                Vector3 v1 = (A - center1).Normalized();
                Vector3 v2 = (B - center1).Normalized();

                Vector3 t3 = Vector3.Cross(v2, v1);

                if (Vector3.Dot(t3, center2 - center1) < 0)
                {
                    Sphere tmp = Sphere1;
                    Sphere1 = Sphere2;
                    Sphere2 = tmp;
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vypocita bod na Coonsovej zaplate na i-tom segmente v case t
        private Vector3 _CoonsPatchPoint(float u, float v, float t)
        {
            return _Sc(u, v, t) + _Sd(u, v, t) - _Scd(u, v, t);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // prva tvoriaca zaplata v case t
        private Vector3 _Sc(float u, float v, float t)
        {
            return MainWindow.H3(0, v) * _c0(u, t) + MainWindow.H3(1, v) * _e(0, u, t) + MainWindow.H3(2, v) * _e(1, u, t) + MainWindow.H3(3, v) * _c1(u, t);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // druha tvoriaca zaplata v case t
        private Vector3 _Sd(float u, float v, float t)
        {
            return MainWindow.H3(0, u) * _d0(v, t) + MainWindow.H3(1, u) * _f(0, v, t) + MainWindow.H3(2, u) * _f(1, v, t) + MainWindow.H3(3, u) * _d1(v, t);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // korekcna zaplata v case t
        private Vector3 _Scd(float u, float v, float t)
        {
            Vector4 Hu = new Vector4(MainWindow.H3(0, u), MainWindow.H3(1, u), MainWindow.H3(2, u), MainWindow.H3(3, u));
            Vector4 Hv = new Vector4(MainWindow.H3(0, v), MainWindow.H3(1, v), MainWindow.H3(2, v), MainWindow.H3(3, v));
            /*Vector3 t00 = Vector3.Zero;
            Vector3 t10 = Vector3.Zero;
            Vector3 t01 = Vector3.Zero;
            Vector3 t11 = Vector3.Zero;*/

            return Hv[0] * (Hu[0] * _c0(0, t) + Hu[1] * _f(0, 0, t) + Hu[2] * _f(1, 0, t) + Hu[3] * _c0(1, t)) +
                   Hv[1] * (Hu[0] * _e(0, 0, t) /*+ Hu[1] * t00 + Hu[2] * t01*/ + Hu[3] * _e(0, 1, t)) +
                   Hv[2] * (Hu[0] * _e(1, 0, t) /*+ Hu[1] * t10 + Hu[2] * t11*/ + Hu[3] * _e(1, 1, t)) +
                   Hv[3] * (Hu[0] * _c1(0, t) + Hu[1] * _f(0, 1, t) + Hu[2] * _f(1, 1, t) + Hu[3] * _c1(1, t));
        }

        // hranicna krivka c0 Coonsovej zaplaty v case t
        private Vector3 _c0(float u, float t)
        {
            _branchedSurface1.TransformSpheresToLocal();

            Vector3 center1 = new Vector3(Sphere1.CenterInTime(0).X, Sphere1.CenterInTime(0).Y, 0.0f);
            Vector3 center2 = new Vector3(Sphere2.CenterInTime(0).X, Sphere2.CenterInTime(0).Y, 0.0f);
            Vector3 v = center2 - center1;

            Vector3 X = MainWindow.HermiteCurve(u, center1, v, v, center2);
            Vector3 surfacePoint = _branchedSurface1.SUp(X.X, X.Y, t);

            _branchedSurface1.TransformSpheresToGlobal();

            return surfacePoint;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka c1 Coonsovej zaplaty na i-tom segmente v case t
        private Vector3 _c1(float u, float t)
        {
            _branchedSurface2.TransformSpheresToLocal();

            Vector3 center1 = new Vector3(Sphere1.CenterInTime(0).X, Sphere1.CenterInTime(0).Y, 0.0f);
            Vector3 center2 = new Vector3(Sphere2.CenterInTime(0).X, Sphere2.CenterInTime(0).Y, 0.0f);
            Vector3 v = center2 - center1;

            Vector3 X = MainWindow.HermiteCurve(u, center1, v, v, center2);
            Vector3 surfacePoint = _branchedSurface2.SUp(X.X, X.Y, t);

            _branchedSurface2.TransformSpheresToGlobal();

            return surfacePoint;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d0 Coonsovej zaplaty case t
        private Vector3 _d0(float v, float t)
        {
            Vector3 center = Sphere1.CenterInTime(t);

            Vector3 B = _c0(0, t);
            Vector3 A = _c1(0, t);
            Vector3 v1 = (A - center).Normalized();
            Vector3 v2 = (B - center).Normalized();

            Vector3 t3 = Vector3.Cross(v2, v1);

            float angle = Vector3.CalculateAngle(v1, v2); // (float)Math.Acos(Vector3.Dot(v1, v2) / (v1.Length * v2.Length));

            Vector3 t2 = Vector3.Cross(t3, v2).Normalized();

            float dt = 1.0f + (1.0f - t) * (Sphere1.R0 / Sphere1.R - 1.0f);
            float Rt = Sphere1.R + (1.0f - t) * (Sphere1.R0 - Sphere1.R);

            return center + Rt * (float)Math.Cos(angle * (double)v) * v2 + Rt * (float)Math.Sin(angle * (double)v) * t2; // v2;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // hranicna krivka d1 Coonsovej zaplaty v case t
        private Vector3 _d1(float v, float t)
        {
            Vector3 center = Sphere2.CenterInTime(t);

            Vector3 B = _c0(1, t);
            Vector3 A = _c1(1, t);
            Vector3 v1 = (A - center).Normalized();
            Vector3 v2 = (B - center).Normalized();

            Vector3 t3 = Vector3.Cross(v2, v1);

            float angle = Vector3.CalculateAngle(v1, v2); // (float)Math.Acos(Vector3.Dot(v1, v2) / (v1.Length * v2.Length));

            Vector3 t2 = Vector3.Cross(t3, v2).Normalized();

            float dit = 1.0f + (1.0f - t) * (Sphere2.R0 / Sphere2.R - 1.0f);
            float Rt = Sphere2.R + (1.0f - t) * (Sphere2.R0 - Sphere2.R);

            return center + Rt * (float)Math.Cos(angle * (double)v) * v2 + Rt * (float)Math.Sin(angle * (double)v) * t2; // v2;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // j-ta (j=0,1) funkcia derivacii ej na bocnej potahovej plochy v case t
        private Vector3 _e(int j, float u, float t)
        {
            if (j == 0)
            {
                _branchedSurface1.TransformSpheresToLocal();
            }
            else
            {
                _branchedSurface2.TransformSpheresToLocal();
            }

            Vector3 center1 = Sphere1.CenterInTime(0);
            Vector3 center2 = Sphere2.CenterInTime(0);
            Vector3 v = center2 - center1;

            Vector3 X = MainWindow.HermiteCurve(u, center1, v, v, center2);

            Vector3 nS;
            Vector3 dcij;

            if (j == 0)
            {
                Vector3 normal = _branchedSurface1.normalSH(X.X, X.Y, t);
                Vector3 dsup = _branchedSurface1.dsup(u, t, Sphere1, Sphere2);
                nS = (_branchedSurface1.InverseMatrix * new Vector4(normal.X, normal.Y, normal.Z, 0.0f)).Xyz;
                dcij = (_branchedSurface1.InverseMatrix * new Vector4(dsup.X, dsup.Y, dsup.Z, 0.0f)).Xyz;

                _branchedSurface1.TransformSpheresToGlobal();
            }
            else
            {
                Vector3 normal = _branchedSurface2.normalSH(X.X, X.Y, t);
                Vector3 dsup = _branchedSurface2.dsup(u, t, Sphere1, Sphere2);
                nS = (_branchedSurface2.InverseMatrix * new Vector4(normal.X, normal.Y, normal.Z, 0.0f)).Xyz;
                dcij = (_branchedSurface2.InverseMatrix * new Vector4(dsup.X, dsup.Y, dsup.Z, 0.0f)).Xyz;

                _branchedSurface2.TransformSpheresToGlobal();
            }

            return MainWindow.Tau * Vector3.Cross(nS, dcij).Normalized();
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // j-ta (j=0,1) funkcia derivacii fj na bocnej potahovej plochy v case t
        private Vector3 _f(int j, float v, float t)
        {
            Vector3 center = j == 0 ? Sphere1.CenterInTime(t) : Sphere2.CenterInTime(t);

            Vector3 B = j == 0 ? _c0(0, t) : _c0(1, t);
            Vector3 A = j == 0 ? _c1(0, t) : _c1(1, t);
            Vector3 v1 = A - center;
            Vector3 v2 = B - center;

            Vector3 n = Vector3.Cross(v1, v2).Normalized();

            if (j == 0) return (float)Math.Pow(Sphere1.R / Sphere2.R, MainWindow.Lambda) * n;
            else return (float)Math.Pow(Sphere2.R / Sphere1.R, MainWindow.Lambda) * n;
        }

        //-----------------------------------------------------------------------------------------------------------------------

        // vypocita normalu vo vrchole (i,j) na segmente bocnej potahovej plochy
        private Vector3 _ComputeNormal(int i, int j)
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

            if (i > 0 && i < _window.Lod1 - 1 && j > 0 && j < _window.Lod1 - 1)
            {
                v1 = Samples[i + 1, j] - Samples[i, j];
                v2 = Samples[i, j + 1] - Samples[i, j];
                v3 = Samples[i - 1, j] - Samples[i, j];
                v4 = Samples[i, j - 1] - Samples[i, j];
                n1 = Vector3.Cross(v1, v2);
                n2 = Vector3.Cross(v2, v3);
                n3 = Vector3.Cross(v3, v4);
                n4 = Vector3.Cross(v4, v1);
                n = (n1 + n2 + n3 + n4) / 4.0f;
            }
            else if (i == 0)
            {
                if (j > 0 && j < _window.Lod1 - 1)
                {
                    v1 = Samples[i + 1, j] - Samples[i, j];
                    v2 = Samples[i, j + 1] - Samples[i, j];
                    v4 = Samples[i, j - 1] - Samples[i, j];
                    n1 = Vector3.Cross(v1, v2);
                    n4 = Vector3.Cross(v4, v1);
                    n = (n1 + n4) / 2.0f;
                }
                else if (j == 0)
                {
                    v1 = Samples[i + 1, j] - Samples[i, j];
                    v2 = Samples[i, j + 1] - Samples[i, j];
                    n = Vector3.Cross(v1, v2);
                }
                else
                {
                    v1 = Samples[i + 1, j] - Samples[i, j];
                    v4 = Samples[i, j - 1] - Samples[i, j];
                    n = Vector3.Cross(v4, v1);
                }
            }
            else if (i == _window.Lod1 - 1)
            {
                if (j > 0 && j < _window.Lod1 - 1)
                {
                    v2 = Samples[i, j + 1] - Samples[i, j];
                    v3 = Samples[i - 1, j] - Samples[i, j];
                    v4 = Samples[i, j - 1] - Samples[i, j];
                    n2 = Vector3.Cross(v2, v3);
                    n3 = Vector3.Cross(v3, v4);
                    n = (n2 + n3) / 2.0f;
                }
                else if (j == 0)
                {
                    v2 = Samples[i, j + 1] - Samples[i, j];
                    v3 = Samples[i - 1, j] - Samples[i, j];
                    n = Vector3.Cross(v2, v3);
                }
                else
                {
                    v3 = Samples[i - 1, j] - Samples[i, j];
                    v4 = Samples[i, j - 1] - Samples[i, j];
                    n = Vector3.Cross(v3, v4);
                }
            }
            else if (j == 0)
            {
                v1 = Samples[i + 1, j] - Samples[i, j];
                v2 = Samples[i, j + 1] - Samples[i, j];
                v3 = Samples[i - 1, j] - Samples[i, j];
                n1 = Vector3.Cross(v1, v2);
                n2 = Vector3.Cross(v2, v3);
                n = (n1 + n2) / 2.0f;
            }
            else if (j == _window.Lod1 - 1)
            {
                v1 = Samples[i + 1, j] - Samples[i, j];
                v3 = Samples[i - 1, j] - Samples[i, j];
                v4 = Samples[i, j - 1] - Samples[i, j];
                n3 = Vector3.Cross(v3, v4);
                n4 = Vector3.Cross(v4, v1);
                n = (n3 + n4) / 2.0f;
            }
            return n;
        }
    }
}