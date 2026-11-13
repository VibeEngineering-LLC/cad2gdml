using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Cad2Gdml.Geometry;
using Cad2Gdml.Import;
using Cad2Gdml.Materials;
using Cad2Gdml.Meshes;
using Cad2Gdml.Scene;

namespace Cad2Gdml.Tests
{
    public static class ChecksTess
    {
        // 6 граней: 4 вершины × 3 координаты, обход против часовой снаружи
        private static readonly int[][] F = {
            new[]{0,0,0, 0,1,0, 1,1,0, 1,0,0},
            new[]{0,0,1, 1,0,1, 1,1,1, 0,1,1},
            new[]{0,0,0, 1,0,0, 1,0,1, 0,0,1},
            new[]{0,1,0, 0,1,1, 1,1,1, 1,1,0},
            new[]{0,0,0, 0,0,1, 0,1,1, 0,1,0},
            new[]{1,0,0, 1,1,0, 1,1,1, 1,0,1},
        };

        internal static double Volume(Mesh m)
        {
            double v = 0;
            for (int i = 0; i < m.TriangleCount; i++)
            { Vec3d a,b,c; m.GetTriangle(i, out a, out b, out c);
              v += a.X*(b.Y*c.Z-b.Z*c.Y)-a.Y*(b.X*c.Z-b.Z*c.X)+a.Z*(b.X*c.Y-b.Y*c.X); }
            return v/6.0;
        }

        private static void CV(string t, Vec3d v, double x, double y, double z)
        { if (Math.Abs(v.X-x)>1e-9||Math.Abs(v.Y-y)>1e-9||Math.Abs(v.Z-z)>1e-9)
          throw new Exception(t+": вершина ("+v.X+","+v.Y+","+v.Z+")!="+x+","+y+","+z); }

        private static void CF(string t, MaterialDef md, string s, double exp)
        { var kv=md.MassFractions.FirstOrDefault(p=>p.Key==s);
          if (kv.Key==null) throw new Exception(t+": элемент "+s+" не найден");
          if (Math.Abs(kv.Value-exp)>1e-9) throw new Exception(t+": "+s+"="+kv.Value+"!="+exp); }

        private static string VN(int x,int y,int z){return"c"+x+y+z;}
        private static string N(double d){return d.ToString("R",CultureInfo.InvariantCulture);}
        private static string PU(string u){return u==null?"":" unit=\""+u+"\"";}

        internal static string CubeGdml(string kind,string posUnit,double posScale,string offUnit,double offScale,string lunit,string missingVertex)
        {
            var sb=new StringBuilder(); var rel=new List<string>(); int rn=0;
            sb.AppendLine("<gdml>"); sb.AppendLine("<define>");
            for(int ix=0;ix<=1;ix++)for(int iy=0;iy<=1;iy++)for(int iz=0;iz<=1;iz++)
            { double x=ix*10*posScale,y=iy*10*posScale,z=iz*10*posScale;
              sb.AppendLine("<position name=\""+VN(ix,iy,iz)+"\" x=\""+N(x)+"\" y=\""+N(y)+"\" z=\""+N(z)+"\""+PU(posUnit)+"/>"); }
            if(kind=="triRel"||kind=="quadRel")
            { for(int fi=0;fi<6;fi++){
                int[] a={F[fi][0],F[fi][1],F[fi][2]},b={F[fi][3],F[fi][4],F[fi][5]},
                  c={F[fi][6],F[fi][7],F[fi][8]},d={F[fi][9],F[fi][10],F[fi][11]};
                void AR(int[]f,int[]t){double dx=(t[0]-f[0])*10*offScale,dy=(t[1]-f[1])*10*offScale,dz=(t[2]-f[2])*10*offScale;
                  string nm="r"+(rn++);sb.AppendLine("<position name=\""+nm+"\" x=\""+N(dx)+"\" y=\""+N(dy)+"\" z=\""+N(dz)+"\""+PU(offUnit)+"/>");rel.Add(nm);}
                if(kind=="triRel"){AR(a,b);AR(a,c);AR(a,c);AR(a,d);}
                else{AR(a,b);AR(a,c);AR(a,d);}
            }}
            sb.AppendLine("</define>");
            sb.AppendLine("<materials>");
            sb.AppendLine("<element name=\"C\" formula=\"C\" Z=\"6\"><atom value=\"12.011\"/></element>");
            sb.AppendLine("<element name=\"H\" formula=\"H\" Z=\"1\"><atom value=\"1.008\"/></element>");
            sb.AppendLine("<element name=\"O\" formula=\"O\" Z=\"8\"><atom value=\"15.999\"/></element>");
            sb.AppendLine("<element name=\"N\" formula=\"N\" Z=\"7\"><atom value=\"14.007\"/></element>");
            sb.AppendLine("<element name=\"Ar\" formula=\"Ar\" Z=\"18\"><atom value=\"39.948\"/></element>");
            sb.AppendLine("<material name=\"PETG\" state=\"solid\"><D value=\"1.27\" unit=\"g/cm3\"/><composite n=\"10\" ref=\"C\"/><composite n=\"8\" ref=\"H\"/><composite n=\"4\" ref=\"O\"/></material>");
            sb.AppendLine("<material name=\"AIR\" state=\"gas\"><D value=\"0.00120479\" unit=\"g/cm3\"/><fraction n=\"0.755267\" ref=\"N\"/><fraction n=\"0.231781\" ref=\"O\"/><fraction n=\"0.012827\" ref=\"Ar\"/><fraction n=\"0.000124\" ref=\"C\"/></material>");
            sb.AppendLine("</materials>");
            string la=lunit==null?"":" lunit=\""+lunit+"\"";
            sb.AppendLine("<solids>"); sb.AppendLine("<tessellated name=\"Cube_solid\" aunit=\"deg\""+la+">");
            int ri=0;
            for(int fi=0;fi<6;fi++)
            {
                string va=VN(F[fi][0],F[fi][1],F[fi][2]),vb=VN(F[fi][3],F[fi][4],F[fi][5]),
                  vc=VN(F[fi][6],F[fi][7],F[fi][8]),vd=VN(F[fi][9],F[fi][10],F[fi][11]);
                if(fi==0&&missingVertex!=null)vb=missingVertex;
                if(kind=="quadAbs") sb.AppendLine("<quadrangular vertex1=\""+va+"\" vertex2=\""+vb+"\" vertex3=\""+vc+"\" vertex4=\""+vd+"\" type=\"ABSOLUTE\"/>");
                else if(kind=="quadDefault") sb.AppendLine("<quadrangular vertex1=\""+va+"\" vertex2=\""+vb+"\" vertex3=\""+vc+"\" vertex4=\""+vd+"\"/>");
                else if(kind=="triAbs"){sb.AppendLine("<triangular vertex1=\""+va+"\" vertex2=\""+vb+"\" vertex3=\""+vc+"\" type=\"ABSOLUTE\"/>");
                  sb.AppendLine("<triangular vertex1=\""+va+"\" vertex2=\""+vc+"\" vertex3=\""+vd+"\" type=\"ABSOLUTE\"/>");}
                else if(kind=="triRel"){sb.AppendLine("<triangular vertex1=\""+va+"\" vertex2=\""+rel[ri]+"\" vertex3=\""+rel[ri+1]+"\" type=\"RELATIVE\"/>");
                  sb.AppendLine("<triangular vertex1=\""+va+"\" vertex2=\""+rel[ri+2]+"\" vertex3=\""+rel[ri+3]+"\" type=\"RELATIVE\"/>");ri+=4;}
                else if(kind=="quadRel"){sb.AppendLine("<quadrangular vertex1=\""+va+"\" vertex2=\""+rel[ri]+"\" vertex3=\""+rel[ri+1]+"\" vertex4=\""+rel[ri+2]+"\" type=\"RELATIVE\"/>");ri+=3;}
            }
            sb.AppendLine("</tessellated>");
            sb.AppendLine("<box name=\"World_solid\" x=\"100\" y=\"100\" z=\"100\" lunit=\"mm\"/>");
            sb.AppendLine("</solids>");
            sb.AppendLine("<structure>");
            sb.AppendLine("<volume name=\"Cube\"><materialref ref=\"PETG\"/><solidref ref=\"Cube_solid\"/></volume>");
            sb.AppendLine("<volume name=\"World\"><materialref ref=\"AIR\"/><solidref ref=\"World_solid\"/><physvol name=\"Cube_pv\"><volumeref ref=\"Cube\"/></physvol></volume>");
            sb.AppendLine("</structure>");
            sb.AppendLine("<setup name=\"Default\" version=\"1.0\"><world ref=\"World\"/></setup>");
            sb.AppendLine("</gdml>"); return sb.ToString();
        }

        private static IList<Part> LoadCube(string dir,string name,string kind,string pu,double ps,
            string ou,double os,string lu,string mv)
        { Directory.CreateDirectory(dir); string f=Path.Combine(dir,name);
          File.WriteAllText(f,CubeGdml(kind,pu,ps,ou,os,lu,mv),new UTF8Encoding(false));
          IList<string>sk; return GdmlImporter.Load(f,out sk); }

        public static List<KeyValuePair<string,Action>> All(string tempDir)
        {
            var L=new List<KeyValuePair<string,Action>>();
            string td=Path.Combine(tempDir,"tess");
            // 1 — загрузка реального выреза
            L.Add(new KeyValuePair<string,Action>("tess_real_cut_loads",()=>{
                Directory.CreateDirectory(td); string f=Path.Combine(td,"t1.gdml");
                File.WriteAllText(f,FixturesTess.RealCut,new UTF8Encoding(false));
                IList<string>sk; IList<Part>ps=GdmlImporter.Load(f,out sk);
                if(ps.Count!=1)throw new Exception("tess_real_cut_loads: деталей="+ps.Count);
                if(sk.Count!=0)throw new Exception("tess_real_cut_loads: skipped не пуст");
                var p=ps[0];
                if(p.Name!="Can")throw new Exception("tess_real_cut_loads: Name="+p.Name);
                if(p.Material!="PETG")throw new Exception("tess_real_cut_loads: Material="+p.Material);
                if(p.Mesh.TriangleCount!=FixturesTess.RealTriangles)throw new Exception("tess_real_cut_loads: TC="+p.Mesh.TriangleCount);
                Vec3d a,b,c;
                p.Mesh.GetTriangle(0,out a,out b,out c);
                CV("tess_real_cut_loads",a,-10.441360,11.725784,36.684711);
                CV("tess_real_cut_loads",b,-10.441360,12.018448,37.470249);
                CV("tess_real_cut_loads",c,-10.835648,11.850601,37.039162);
                p.Mesh.GetTriangle(1,out a,out b,out c);
                CV("tess_real_cut_loads",a,-10.835648,11.850601,37.039162);
                CV("tess_real_cut_loads",b,-10.441360,12.018448,37.470249);
                CV("tess_real_cut_loads",c,-11.253510,12.003700,37.470249);
                if(MeshWelder.Weld(p.Mesh,1e-9).Vertices.Count!=26)throw new Exception("tess_real_cut_loads: Vertices!=26");
            }));
            // 2 — композит PETG и AIR
            L.Add(new KeyValuePair<string,Action>("tess_real_cut_composite_petg",()=>{
                Directory.CreateDirectory(td); string f=Path.Combine(td,"t2.gdml");
                File.WriteAllText(f,FixturesTess.RealCut,new UTF8Encoding(false));
                List<string>notes; List<MaterialDef>ms=GdmlMaterials.Load(f,out notes);
                var pg=ms.FirstOrDefault(m=>m.Name=="PETG");
                if(pg==null)throw new Exception("tess_real_cut_composite_petg: PETG нет");
                if(Math.Abs(pg.DensityGcm3-1.27)>1e-9)throw new Exception("tess_real_cut_composite_petg: D");
                if(pg.IsNist)throw new Exception("tess_real_cut_composite_petg: IsNist");
                if(notes.Any(n=>n.Contains("PETG")))throw new Exception("tess_real_cut_composite_petg: notes PETG");
                double mw=10*12.011+8*1.008+4*15.999;
                // независимый расчёт C10H8O4 (M=192.17 г/моль), не по формуле кода
                CF("tess_real_cut_composite_petg",pg,"C",0.625019513972004);
                CF("tess_real_cut_composite_petg",pg,"H",0.04196284539730447);
                CF("tess_real_cut_composite_petg",pg,"O",0.3330176406306916);
                if(Math.Abs(pg.MassFractions.Sum(k=>k.Value)-1)>1e-12)throw new Exception("tess_real_cut_composite_petg: сумма");
                var ar=ms.FirstOrDefault(m=>m.Name=="AIR");
                if(ar==null)throw new Exception("tess_real_cut_composite_petg: AIR нет");
                if(Math.Abs(ar.MassFractions.First(k=>k.Key=="N").Value-0.755267/0.999999)>1e-9)throw new Exception("tess_real_cut_composite_petg: N AIR (доли файла в сумме 0.999999, нормируются на 1)");
            }));
            // 3 — куб: все режимы
            L.Add(new KeyValuePair<string,Action>("tess_cube_rules",()=>{
                string[]kinds={"quadAbs","triAbs","triRel","quadRel","quadDefault"};
                foreach(var k in kinds){
                    var ps=LoadCube(td,"t3_"+k+".gdml",k,"mm",1,"mm",1,"mm",null);
                    if(ps.Count!=1)throw new Exception("tess_cube_rules: "+k+" деталей="+ps.Count);
                    var m=ps[0].Mesh;
                    if(m.TriangleCount!=12)throw new Exception("tess_cube_rules: "+k+" TC="+m.TriangleCount);
                    double vol=Volume(m); if(Math.Abs(vol-1000)>1e-6)throw new Exception("tess_cube_rules: "+k+" V="+vol);
                    Vec3d mn,mx; m.GetBounds(out mn,out mx);
                    if(Math.Abs(mn.X)>1e-9||Math.Abs(mn.Y)>1e-9||Math.Abs(mn.Z)>1e-9||
                       Math.Abs(mx.X-10)>1e-9||Math.Abs(mx.Y-10)>1e-9||Math.Abs(mx.Z-10)>1e-9)
                        throw new Exception("tess_cube_rules: "+k+" bounds");
                    if(!MeshTopology.Analyze(MeshWelder.Weld(m,1e-9)).IsClosedManifold)
                        throw new Exception("tess_cube_rules: "+k+" не замкнут");
                }
            }));
            // 4 — единицы измерения
            L.Add(new KeyValuePair<string,Action>("tess_cube_units",()=>{
                void T2(string tag,string kind,string pu,double ps,string ou,double os,string lu,double expected,double tol){
                    var parts=LoadCube(td,"t4_"+tag+".gdml",kind,pu,ps,ou,os,lu,null);
                    if(parts.Count!=1)throw new Exception("tess_cube_units: "+tag+" деталей="+parts.Count);
                    double vol=Volume(parts[0].Mesh);
                    if(Math.Abs(vol-expected)>tol)throw new Exception("tess_cube_units: "+tag+" V="+vol+"!="+expected);
                }
                // (а) posUnit cm, posScale 0.1
                T2("a","quadAbs","cm",0.1,"mm",1,"mm",1000,1e-6);
                // (б) posUnit null, posScale 1, lunit null
                T2("b","quadAbs",null,1,"mm",1,null,1000,1e-6);
                // (в) lunit ТЕЛА по Geant4 не применяется (единица берётся с граней) → объём 1000
                T2("c","quadAbs","mm",1,"mm",1,"cm",1000,1e-6);
                // (г) triRel, offUnit cm, offScale 0.1
                T2("d","triRel","mm",1,"cm",0.1,"mm",1000,1e-6);
                // (д) quadRel, offUnit cm, offScale 0.1
                T2("e","quadRel","mm",1,"cm",0.1,"mm",1000,1e-6);
            }));
            // 5 — отсутствующая вершина
            L.Add(new KeyValuePair<string,Action>("tess_missing_vertex",()=>{
                Directory.CreateDirectory(td); string f=Path.Combine(td,"t5.gdml");
                File.WriteAllText(f,CubeGdml("quadAbs","mm",1,"mm",1,"mm","nope_v7"),new UTF8Encoding(false));
                try{ IList<string>sk; GdmlImporter.Load(f,out sk);
                  throw new Exception("tess_missing_vertex: исключение не брошено"); }
                catch(InvalidDataException e){
                  if(!e.Message.Contains("nope_v7")||!e.Message.Contains("Cube_solid"))
                    throw new Exception("tess_missing_vertex: сообщение: "+e.Message); }
                catch(Exception e){
                  throw new Exception("tess_missing_vertex: неверный тип исключения: "+e.GetType().Name+" "+e.Message); }
            }));
            // 6 — крайние случаи композита
            L.Add(new KeyValuePair<string,Action>("tess_composite_edge_cases",()=>{
                Directory.CreateDirectory(td);
                string base2=CubeGdml("quadAbs","mm",1,"mm",1,"mm",null);
                string extra="<element name=\"Xx\" formula=\"Xx\" Z=\"99\"/>"+
                  "<material name=\"NOATOM\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"1\" ref=\"Xx\"/></material>"+
                  "<material name=\"BADREF\" state=\"solid\"><D value=\"1.0\" unit=\"g/cm3\"/><composite n=\"2\" ref=\"Nope\"/></material>";
                string txt=base2.Replace("</materials>",extra+"</materials>");
                string f=Path.Combine(td,"t6.gdml");
                File.WriteAllText(f,txt,new UTF8Encoding(false));
                List<string>notes; List<MaterialDef>ms=GdmlMaterials.Load(f,out notes);
                if(ms.Any(m=>m.Name=="NOATOM"))throw new Exception("tess_composite_edge_cases: NOATOM нашёлся");
                if(ms.Any(m=>m.Name=="BADREF"))throw new Exception("tess_composite_edge_cases: BADREF нашёлся");
                if(!ms.Any(m=>m.Name=="PETG"))throw new Exception("tess_composite_edge_cases: PETG пропал");
                if(!notes.Any(n=>n.Contains("NOATOM")&&n.Contains("атомной массы")))
                    throw new Exception("tess_composite_edge_cases: notes NOATOM");
                if(!notes.Any(n=>n.Contains("BADREF")))
                    throw new Exception("tess_composite_edge_cases: notes BADREF");
            }));
            // 7 — roundtrip через CheckUtil.Run
            L.Add(new KeyValuePair<string,Action>("tess_roundtrip_cube",()=>{
                Directory.CreateDirectory(td); string f=Path.Combine(td,"t7_in.gdml");
                File.WriteAllText(f,CubeGdml("quadAbs","mm",1,"mm",1,"mm",null),new UTF8Encoding(false));
                var r=CheckUtil.Run(td,new[]{f},1.0,"+z",true,false);
                if(!File.Exists(r.GdmlPath))
                    throw new Exception("tess_roundtrip_cube: ExitCode="+r.ExitCode+" "+r.MainErrorMessage);
                IList<string>sk; IList<Part>ps=GdmlImporter.Load(r.GdmlPath,out sk);
                if(ps.Count<1)throw new Exception("tess_roundtrip_cube: деталей<1");
                double tv=ps.Sum(p=>Volume(p.Mesh));
                if(Math.Abs(tv-1000)>1e-3)throw new Exception("tess_roundtrip_cube: V="+tv);
                List<string>notes2; List<MaterialDef>ms=GdmlMaterials.Load(r.GdmlPath,out notes2);
                var pg=ms.FirstOrDefault(m=>m.Name=="PETG");
                if(pg==null)throw new Exception("tess_roundtrip_cube: PETG нет в выходе");
                double mw=10*12.011+8*1.008+4*15.999;
                void CF6(string s,double exp){var kv=pg.MassFractions.FirstOrDefault(p=>p.Key==s);
                  if(kv.Key==null||Math.Abs(kv.Value-exp)>1e-6)throw new Exception("tess_roundtrip_cube: "+s);}
                CF6("C",10*12.011/mw); CF6("H",8*1.008/mw); CF6("O",4*15.999/mw);
            }));
            return L;
        }
    }
}
