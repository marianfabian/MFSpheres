			//////////////////////////////////////////////////////////////////
			// 						                //
			//      Informations about using the MFSpheres application      //
			// 					    	                //
			//////////////////////////////////////////////////////////////////

CREATION OF SPHERES:
	- click (click + drag) with the left mouse button in the plane z=0
	- branched surface works for convex and nonconvex polygons given by the centres of spheres, which are arranged in the counterclockwise direction,
	  if they are arranged differently then the spheres have to be moved
	- the tubular surface works also for a sequence of spheres, it is possible to modify the vectors n[i] (by clicking with the left mouse button and dragging)

MODIFICATION OF THE SPHERES:
	- clicking (with the left button) on any created sphere and dragging will change the radius
	- if the M key is pressed while clicking on the sphere, the whole sphere will move (its center is at the cursor position, in the z=0 plane)
        - if the numeric key + or - is pressed when clicking on the sphere, the whole sphere is moved along the [O, C - O] axis
	  with a step of 0.01 to or from the center of the coordinate system
        - if the S or D key is pressed while clicking on the sphere, the radius of the sphere increases or decreases with a step of 0.01
        - delete a sphere by double-clicking (left-clicking) the mouse on it

SURFACE SETTINGS:
	- the corresponding settings modify the skinning surface
	- each slider changes the corresponding parameters
	- first of all it is necessary to choose which type of construction of the skinning surface is to be calculated (by default it is set to be branched by homotopy)
	- control vectors are only visible if the tubular construction is set
	- the number of samples of the side part of the surface also determines the number of samples on each segment of the tubular skinning surface
        - parameter Lambda from the interval <0, 5> determines the size of the fij vectors as follows: |fi0| = (ri/r(i+1))^Lambda, |fi1| = (r(i+1)/ri)^Lambda, 
				                                                                      |fij| = (r(i+j%2)/r(i+(j+1)%2)^Lambda, j=0,1
        - parameter Tau is from the interval <0.05, 5> and determines the size of the vectors eij

ENVIRONMENT SETTINGS:
        - the coordinate system, the grid of the plane z=0, created spheres can be displayed and their transparency can be changed
        - the user can change the color of the background, skinning surface and spheres from the selected options

MOVEMENT IN SPACE:
	- clicking and dragging with the right mouse button - rotation 
	- mouse wheel - zooming
	- by pressing the key:
	   - X: side view (on the yz plane)
           - Y: side view (on the xz plane)
           - Z: top view (on the xy plane)
        - when hovering the mouse over a sphere, info about the sphere is displayed in the lower text box

CLEAR THE SCENE:
        - the corresponding button removes all spheres, removes the dodecahedron example and the control vectors but keeps the other parameters of the surface


			//////////////////////////////////////////////////////////
			// 						        //
			//      Informacie o pouzivani aplikacie MFSpheres      //
			// 					    	        //
			//////////////////////////////////////////////////////////

VYTVARANIE SFER:
	- kliknutim (kliknutim + potiahnutim) lavym tlacidlom mysi do roviny z=0
	- rozvetvena plocha funguje pre konvexne a nekonvexne mnohouholniky dane stredmi sfer,
	  ktore su usporiadane v protismere hodinovych ruciciek, ak su zadane inak tak je potom treba presunut sfery
	- tubularna plocha funguje tiez pre postupnost sfer, je mozna modifikacia vektorov n[i] (kliknutim lavym tlacidlom mysi a tahanim)

MODIFIKACIA SFER:
	- kliknutim (lavym tlacidlom) na ktorukolvek vytvorenu sferu a naslednym tahanim sa meni jej polomer
	- ak je pri kliknuti na sferu stlacena klavesa M tak sa posuva cela sfera (jej stred je v mieste kurzora, v rovine z=0)
	- ak je pri kliknuti na sferu stlacena numericka klavesa + alebo - tak sa posuva cela sfera po osi [O, C - O] s krokom 0.01 ku alebo od stredu suradnicovej sustavy
	- ak je pri kliknuti na sferu stlacena klavesa S alebo D tak sa zvacsiuje alebo zmensuje polomer sfery s krokom 0.01
	- zmazanie sfery dvojkliknutim (lavym tlacidlom) mysi na nej

NASTAVENIA PLOCHY:
	- prislusne nastavenia modifikuju potahovu plochu  
	- jednotlive slidere menia prislusne parametre
	- treba najprv zvolit aky typ konstrukcie potahovej plochy ma byt pocitany (defaultne je nastavena rozvetvena pomocou homotopie)
	- riadiace vektory sa zobrazia iba ak je nastavena tubularna konstrukcia
	- pocet vzoriek bocnej casti plochy urcuje aj pocet vzoriek na kazdom segmente tubularnej potahovej plochy
	- parameter Lambda z intervalu <0, 5> urcuje velkost vektorov fij nasledovne: |fi0| = (ri/r(i+1))^Lambda, |fi1| = (r(i+1)/ri)^Lambda, 
								                      |fij| = (r(i+j%2)/r(i+(j+1)%2)^Lambda, j=0,1
	- parameter Tau je z intervalu <0.05, 5> a urcuje velkost vektorov eij

NASTAVENIA PROSTREDIA:
	- da sa zobrazit suradnicova sustava, siet roviny z=0, vytvorene sfery a da sa menit ich priesvitnost
	- pouzivatel moze menit farbu pozadia, potahovej plochy a sfer z vybranych moznosti

POHYB V PRIESTORE:
	- pravym tlacidlom mysi - rotacia 
	- kolieskom mysi - zoomovanie
	- stlacenim klavesy: 
	   - X: pohlad z jedneho boku (na rovinu yz)
           - Y: pohlad z druheho boku (na rovinu xz)
           - Z: pohlad zhora (na rovinu xy)
	- pri bohybe mysou nad nejakou sferou sa zobrazi info o danej sfere v dolnom textovom poli

VYMAZANIE SCENY:
	- prislusne tlacidlo odstrani vsetky sfery, odstrani priklad dvanaststena a riadiace vektory ale zachovava ostatne parametre plochy