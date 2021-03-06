BASIC=profiling.fs util.fsi util.fs bytes.fsi bytes.fs range.fsi range.fs getoptsimple.fsi getoptsimple.fs options.fs unionfind.fsi unionfind.fs 
BASIC_SRC=$(addprefix basic/, $(BASIC))

ABSYN=syntax.fs const.fs visit.fs util.fs print.fs 
ABSYN_SRC=$(addprefix absyn/, $(ABSYN))

FORMAT=format.fsi format.fs
FORMAT_SRC=$(addprefix format/, $(FORMAT))

BACKEND=ocaml.fs
BACKEND_SRC=$(addprefix backend/, $(BACKEND))

PARSER=ast.fs parse.fs lexhelp.fs lex.fs dsenv.fs desugar.fs parseit.fsi parseit.fs driver.fs
PARSER_SRC=$(addprefix parser/, $(PARSER))
PARSER_NOGEN=ast.fs parse.fsy lexhelp.fs lex.fsl dsenv.fs desugar.fs parseit.fsi parseit.fs driver.fs
PARSER_NOGEN_SRC=$(addprefix parser/, $(PARSER_NOGEN))

TCCORE=tcenv.fsi tcenv.fs tcutil.fsi tcutil.fs treln.fsi treln.fs krivine.fs
TCCORE_SRC=$(addprefix tccore/, $(TCCORE))

MONADIC=monadicUtils.fs monadicInference.fs monadic.fs
MONADIC_SRC=$(addprefix monadic/, $(MONADIC))

Z3ENCODE=term.fs env.fs basicdt.fs decodetyp.fs encodingdt.fs query.fs
Z3ENCODE_SRC=$(addprefix z3encode/, $(Z3ENCODE))

TC=errors.fs tcenv.fs tcutil.fs pretype.fs
TC_SRC=$(addprefix tc/, $(TC))

PLATFORM=
BUILD_VERSION=
OTHERFLAGS=--optimize+ --debug:full --nologo -g -O --mlcompatibility $(addprefix --define:, $(BUILD_VERSION))

LIB=../lib
BIN=../bin
ALL_DLLS=basic.dll format.dll absyn.dll backend.dll parser.dll tc.dll #tccore.dll tc.dll z3encode.dll monadic.dll
FSHARP_LIBS=$(BIN)/FSharp.PowerPack.dll $(BIN)/FSharp.PowerPack.Compatibility.dll 
OTHER_DLLS=
BASELIBS=$(addprefix -r , $(FSHARP_LIBS) $(OTHER_DLLS))
RUNTIMEFLAGS=-g --nowarn:62

ifeq ($(PLATFORM), X86)
override OTHERFLAGS += --platform:x86
endif

all: $(BIN)/fstar.exe

z3_x86:
	cp $(BIN)/Microsoft.Z3.x86.dll $(BIN)/Microsoft.Z3.dll
	cp $(BIN)/z3.x86.dll $(BIN)/z3.dll

z3_x64:
	cp $(BIN)/Microsoft.Z3.x64.dll $(BIN)/Microsoft.Z3.dll
	cp $(BIN)/z3.x64.dll $(BIN)/z3.dll


WC: $(BASIC_SRC) $(ABSYN_SRC) parser/ast.fs parser/parse.fsy parser/lexhelp.fs parser/lex.fsl parser/dsenv.fs parser/desugar.fs parser/driver.fs $(TCCORE_SRC) $(MONADIC_SRC) $(Z3ENCODE_SRC) 
	wc -l $^

parser/parse.fs: parser/parse.fsy
	fsyacc --module Microsoft.FStar.Parser.Parse $^

parser/lex.fs: parser/lex.fsl
	fslex --unicode $^

BASIC_DEPS=
$(BIN)/basic.dll: $(BASIC_DEPS) $(BASIC_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(BASIC_DEPS)) $(BASIC_SRC)

ABSYN_DEPS=$(addprefix $(BIN)/, basic.dll)
$(BIN)/absyn.dll: $(ABSYN_DEPS) $(ABSYN_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(ABSYN_DEPS)) $(ABSYN_SRC)

FORMAT_DEPS=
$(BIN)/format.dll: $(FORMAT_DEPS) $(FORMAT_SRC)
	fsc $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(FORMAT_DEPS)) $(FORMAT_SRC)

BACKEND_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll format.dll)
$(BIN)/backend.dll: $(BACKEND_DEPS) $(BACKEND_SRC)
	fsc $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(BACKEND_DEPS)) $(BACKEND_SRC)

PARSER_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll)
$(BIN)/parser.dll: $(PARSER_DEPS) $(PARSER_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(PARSER_DEPS)) $(PARSER_SRC)

TCCORE_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll desugar.dll)
$(BIN)/tccore.dll: $(TCCORE_DEPS) $(TCCORE_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(TCCORE_DEPS)) $(TCCORE_SRC)

Z3ENCODE_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll tccore.dll) 
$(BIN)/z3encode.dll: $(Z3ENCODE_DEPS) $(Z3ENCODE_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) -r $(BIN)/Microsoft.Z3.dll $(addprefix -r , $(Z3ENCODE_DEPS)) $(Z3ENCODE_SRC)

TC_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll)
$(BIN)/tc.dll: $(TC_DEPS) $(TC_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(TC_DEPS)) $(TC_SRC)

MONADIC_DEPS=$(addprefix $(BIN)/, basic.dll absyn.dll tccore.dll tc.dll z3encode.dll)
$(BIN)/monadic.dll: $(MONADIC_DEPS) $(MONADIC_SRC)
	fsc  $(OTHERFLAGS) -a -o $@ $(BASELIBS) $(addprefix -r , $(MONADIC_DEPS)) $(MONADIC_SRC)

FSTAR_DEPS=$(addprefix $(BIN)/, $(ALL_DLLS))
$(BIN)/fstar.exe: $(FSTAR_DEPS) fstar.fs 
	fsc  $(OTHERFLAGS) -o $@ $(BASELIBS) $(addprefix -r , $(FSTAR_DEPS)) fstar.fs 

fstar-nodeps: fstar.fs 
	fsc $(OTHERFLAGS) -o $(BIN)/fstar.exe $(BASELIBS) $(addprefix -r , $(FSTAR_DEPS)) fstar.fs 

check: $(BIN)/fstar.exe 
	rm -rf $(STAGE)
	mkdir -p $(STAGE)/queries
	cp -r $(addprefix $(BIN)/, $(ALL_DLLS) Prims.dll ProofLib.dll fstar.exe) $(STAGE)/
	cp -r $(OTHER_DLLS) $(BIN)/FSharp.Compiler.dll $(BIN)/Microsoft.Z3.dll $(BIN)/z3.dll $(STAGE)/
	$(STAGE)/fstar --fstar_home .. --cert --gadt --prims ../lib/prims_wotuples.f9 certify/coretyping.fst 
	$(STAGE)/fstar --fstar_home .. --rdcil_z3eq --gadt --genIL --writePrims --odir $(STAGE)/queries --prims certify/prims.fst $(CERTIFY_SRC)

wc: $(BASIC_SRC) $(ABSYN_SRC) $(PARSER_NOGEN_SRC) $(TC_SRC) fstar.fs
	wc -l $^

tidy: 
	rm -f $(BIN)/basic.dll

clean:
	@rm -rf *.exe $(addprefix $(BIN)/, $(ALL_DLLS) fstar.exe) *~ *res $(BIN)/*.pdb  $(BIN)/queries
