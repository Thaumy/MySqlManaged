module dbm_test.PgSql.Sync.get.getCol

open NUnit.Framework
open dbm_test.PgSql
open dbm_test.PgSql.Sync.init
open fsharper.types
open fsharper.types.Ord
open fsharper.op.Boxing

[<OneTimeSetUp>]
let OneTimeSetUp () = com.connect ()

[<SetUp>]
let SetUp () = init ()

[<Test>]
let getCol_overload1_test () =
    let result =
        com
            .managed
            .unwrap()
            .getCol ($"SELECT col3 FROM {com.tab1}", 0u)
        |> unwrap2

    for i in 0 .. 49 do
        Assert.AreEqual("init[001,050]", result.[i])



[<Test>]
let getCol_overload2_test () =
    let result =
        let paras: (string * obj) list = [ ("col3", "init[050,100]") ]

        com
            .managed
            .unwrap()
            .getCol ($"SELECT col3 FROM {com.tab1} WHERE col3 = :col3", 0u, paras)
        |> unwrap2

    for i in 0 .. 49 do
        Assert.AreEqual("init[050,100]", result.[i])

//overload2 is based on overload3

[<Test>]
let getCol_overload4_test () =
    let result =
        com
            .managed
            .unwrap()
            .getCol ($"SELECT col3 FROM {com.tab1} WHERE col3 = 'init[001,050]'", "col3")
        |> unwrap2

    for it in result do
        Assert.AreEqual("init[001,050]", it)



[<Test>]
let getCol_overload5_test () =
    let result =
        let paras: (string * obj) list = [ ("col3", "init[050,100]") ]

        com
            .managed
            .unwrap()
            .getCol ($"SELECT col3 FROM {com.tab1} WHERE col3 = :col3", "col3", paras)
        |> unwrap2

    for i in 0 .. 49 do
        Assert.AreEqual("init[050,100]", result.[i])


//overload5 is based on overload6