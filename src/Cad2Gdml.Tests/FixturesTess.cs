namespace Cad2Gdml.Tests
{
    // Сгенерировано scripts/make_tess_fixture.py из рабочего файла (первые 24 треугольников, как в файле)
    internal static class FixturesTess
    {
        internal const int RealTriangles = 24;
        internal const string RealCut = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<gdml>
  <define>
    <position name=""can_v0"" unit=""mm"" x=""-10.441360"" y=""11.725784"" z=""36.684711""/>
    <position name=""can_v1"" unit=""mm"" x=""-10.441360"" y=""12.018448"" z=""37.470249""/>
    <position name=""can_v2"" unit=""mm"" x=""-10.835648"" y=""11.850601"" z=""37.039162""/>
    <position name=""can_v3"" unit=""mm"" x=""-11.253510"" y=""12.003700"" z=""37.470249""/>
    <position name=""can_v4"" unit=""mm"" x=""-11.233765"" y=""11.839981"" z=""37.039162""/>
    <position name=""can_v5"" unit=""mm"" x=""-11.634766"" y=""11.821175"" z=""37.039162""/>
    <position name=""can_v6"" unit=""mm"" x=""-11.625931"" y=""11.668343"" z=""36.603672""/>
    <position name=""can_v7"" unit=""mm"" x=""-12.025360"" y=""11.640686"" z=""36.603672""/>
    <position name=""can_v8"" unit=""mm"" x=""-12.014053"" y=""11.501160"" z=""36.163780""/>
    <position name=""can_v9"" unit=""mm"" x=""-12.806564"" y=""11.416206"" z=""36.163780""/>
    <position name=""can_v10"" unit=""mm"" x=""-12.789642"" y=""11.290588"" z=""35.719898""/>
    <position name=""can_v11"" unit=""mm"" x=""-13.180206"" y=""11.231956"" z=""35.719898""/>
    <position name=""can_v12"" unit=""mm"" x=""-13.160126"" y=""11.110748"" z=""35.232414""/>
    <position name=""can_v13"" unit=""mm"" x=""-13.567856"" y=""11.161201"" z=""35.719898""/>
    <position name=""can_v14"" unit=""mm"" x=""-13.543777"" y=""11.040733"" z=""35.232414""/>
    <position name=""can_v15"" unit=""mm"" x=""-14.004425"" y=""11.064774"" z=""35.719898""/>
    <position name=""can_v16"" unit=""mm"" x=""-13.975388"" y=""10.945389"" z=""35.232414""/>
    <position name=""can_v17"" unit=""mm"" x=""-14.382355"" y=""10.837044"" z=""35.232414""/>
    <position name=""can_v18"" unit=""mm"" x=""-14.352493"" y=""10.734352"" z=""34.741196""/>
    <position name=""can_v19"" unit=""mm"" x=""-14.730179"" y=""10.614960"" z=""34.741196""/>
    <position name=""can_v20"" unit=""mm"" x=""-14.700684"" y=""10.528999"" z=""34.246765""/>
    <position name=""can_v21"" unit=""mm"" x=""-15.049530"" y=""10.399460"" z=""34.246765""/>
    <position name=""can_v22"" unit=""mm"" x=""-15.021729"" y=""10.330170"" z=""33.749649""/>
    <position name=""can_v23"" unit=""mm"" x=""-15.342300"" y=""10.191376"" z=""33.749649""/>
    <position name=""can_v24"" unit=""mm"" x=""-15.317642"" y=""10.138542"" z=""33.250366""/>
    <position name=""can_v25"" unit=""mm"" x=""-15.610641"" y=""9.991302"" z=""33.250366""/>
  </define>
  <materials>
    <element name=""H"" formula=""H"" Z=""1""><atom value=""1.008""/></element>
    <element name=""C"" formula=""C"" Z=""6""><atom value=""12.011""/></element>
    <element name=""O"" formula=""O"" Z=""8""><atom value=""15.999""/></element>
    <element name=""N"" formula=""N"" Z=""7""><atom value=""14.007""/></element>
    <element name=""Ar"" formula=""Ar"" Z=""18""><atom value=""39.948""/></element>
    <material name=""PETG"" state=""solid""><D value=""1.27"" unit=""g/cm3""/><composite n=""10"" ref=""C""/><composite n=""8"" ref=""H""/><composite n=""4"" ref=""O""/></material>
    <material name=""AIR"" state=""gas""><D value=""0.00120479"" unit=""g/cm3""/><fraction n=""0.755267"" ref=""N""/><fraction n=""0.231781"" ref=""O""/><fraction n=""0.012827"" ref=""Ar""/><fraction n=""0.000124"" ref=""C""/></material>
  </materials>
  <solids>
    <tessellated name=""Body_solid"" aunit=""deg"" lunit=""mm"">
      <triangular vertex1=""can_v0"" vertex2=""can_v1"" vertex3=""can_v2"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v2"" vertex2=""can_v1"" vertex3=""can_v3"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v2"" vertex2=""can_v3"" vertex3=""can_v4"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v4"" vertex2=""can_v3"" vertex3=""can_v5"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v4"" vertex2=""can_v5"" vertex3=""can_v6"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v6"" vertex2=""can_v5"" vertex3=""can_v7"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v6"" vertex2=""can_v7"" vertex3=""can_v8"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v8"" vertex2=""can_v7"" vertex3=""can_v9"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v8"" vertex2=""can_v9"" vertex3=""can_v10"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v10"" vertex2=""can_v9"" vertex3=""can_v11"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v10"" vertex2=""can_v11"" vertex3=""can_v12"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v12"" vertex2=""can_v11"" vertex3=""can_v13"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v12"" vertex2=""can_v13"" vertex3=""can_v14"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v14"" vertex2=""can_v13"" vertex3=""can_v15"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v14"" vertex2=""can_v15"" vertex3=""can_v16"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v16"" vertex2=""can_v15"" vertex3=""can_v17"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v16"" vertex2=""can_v17"" vertex3=""can_v18"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v18"" vertex2=""can_v17"" vertex3=""can_v19"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v18"" vertex2=""can_v19"" vertex3=""can_v20"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v20"" vertex2=""can_v19"" vertex3=""can_v21"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v20"" vertex2=""can_v21"" vertex3=""can_v22"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v22"" vertex2=""can_v21"" vertex3=""can_v23"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v22"" vertex2=""can_v23"" vertex3=""can_v24"" type=""ABSOLUTE""/>
      <triangular vertex1=""can_v24"" vertex2=""can_v23"" vertex3=""can_v25"" type=""ABSOLUTE""/>
    </tessellated>
    <box name=""World_solid"" x=""100"" y=""100"" z=""100"" lunit=""mm""/>
  </solids>
  <structure>
    <volume name=""Can""><materialref ref=""PETG""/><solidref ref=""Body_solid""/></volume>
    <volume name=""World""><materialref ref=""AIR""/><solidref ref=""World_solid""/>
      <physvol name=""Can_pv""><volumeref ref=""Can""/></physvol>
    </volume>
  </structure>
  <setup name=""Default"" version=""1.0""><world ref=""World""/></setup>
</gdml>
";
    }
}
