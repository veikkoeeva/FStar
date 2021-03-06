﻿(*
   Copyright 2008-2014 Nikhil Swamy and Microsoft Research

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*)
#light "off"
module Microsoft.FStar.Tc.PreType

open Microsoft.FStar
open Microsoft.FStar.Tc
open Microsoft.FStar.Util
open Microsoft.FStar.Absyn
open Microsoft.FStar.Absyn.Syntax
open Microsoft.FStar.Absyn.Util

let rng env = Tc.Env.get_range env
let instantiate_both env = {env with Env.instantiate_targs=true; Env.instantiate_vargs=true}

let check_expected_typ env e t1 = 
  match Env.expected_typ env with 
  | None -> e, t1
  | Some t2 -> Tc.Util.check_and_ascribe env e t1 t2, t2

let rec tc_kind env k : kind = 
  let k = Util.compress_kind k in 
  match k with
  | Kind_uvar _
  | Kind_star -> k

  | Kind_tcon (aopt, k1, k2, imp) -> 
    let k1' = tc_kind env k1 in 
    let env' = match aopt with 
      | None -> env 
      | Some a -> Env.push_local_binding env (Env.Binding_typ(a, k1')) in
    let k2' = tc_kind env' k2 in
    Kind_tcon(aopt, k1', k2', imp)

  | Kind_dcon (xopt, t1, k2, imp) ->
    let t1', _ = tc_typ env t1 in
    let env' = match xopt with 
      | None -> env
      | Some x -> Env.push_local_binding env (Env.Binding_var(x, t1')) in
    let k2' = tc_kind env' k2 in
    Kind_dcon(xopt, t1', k2', imp)

  | Kind_unknown -> 
    Tc.Util.new_kvar env

and tc_typ env t : typ * kind = 
  let env = Tc.Env.set_range env (Util.range_of_typ t (Tc.Env.get_range env)) in
  let t = Util.compress_typ t in
  match t with 
  | Typ_btvar a -> 
    let k = Env.lookup_btvar env a in
    t, k

  | Typ_const i -> 
    let k = Env.lookup_typ_lid env i.v in 
    t, k

  | Typ_fun(xopt, t1, t2, imp) -> 
    let t1' = tc_typ_check env t1 Kind_star in
    let env' = match xopt with 
      | None -> env
      | Some x -> Env.push_local_binding env (Env.Binding_var(x, t1')) in
    let t2' = tc_typ_check env' t2 Kind_star in
    Typ_fun(xopt, t1', t2', imp), Kind_star

  | Typ_univ(a, k1, t1) -> 
    let k1' = tc_kind env k1 in 
    let env' = Env.push_local_binding env (Env.Binding_typ(a, k1')) in
    let t1' = tc_typ_check env' t1 Kind_star in 
    Typ_univ(a, k1', t1'), Kind_star

  | Typ_refine(x, t1, t2) -> 
    let t1' = tc_typ_check env t1 Kind_star in
    let env' = Env.push_local_binding env (Env.Binding_var(x, t1')) in
    let t2' = tc_typ_check env' t2 Kind_star in
    Typ_refine(x, t1', t2'), Kind_star

  | Typ_app(t1, t2, imp) -> 
    let t1', k1' = tc_typ env t1 in 
    let aopt, karg, kres, t1' = match Tc.Util.destruct_tcon_kind env k1' t1' imp with 
      | Kind_tcon(aopt, karg, kres, _), t1' -> aopt, karg, kres, t1'
      | _ -> failwith "impossible" in
    let t2' = tc_typ_check env t2 karg in
    let k2 = match aopt with 
      | None -> kres
      | Some a -> Util.subst_kind [Inl(a, t2')] kres in
    Typ_app(t1', t2', imp), k2
    
  | Typ_dep(t1, e1, imp) -> 
    let t1', k1' = tc_typ env t1 in
    let xopt, targ, kres, t1' = match Tc.Util.destruct_dcon_kind env k1' t1' imp with 
      | Kind_dcon(xopt, targ, kres, _), t1' -> xopt, targ, kres, t1'
      | _ -> failwith "impossible" in
    let env' = Env.set_expected_typ env targ in
    let e1', _ = tc_exp env' e1 in
    let k2 = match xopt with 
      | None -> kres
      | Some x -> Util.subst_kind [Inr(x, e1')] kres in
    Typ_dep(t1', e1', imp), k2
  
  | Typ_lam(x, t1, t2) -> 
    let t1', k1 = tc_typ env t1 in 
    let env' = Env.push_local_binding env (Env.Binding_var(x, t1')) in
    let t2', k2 = tc_typ env' t2 in
    Typ_lam(x, t1', t2'), Kind_dcon(Some x, t1', k2, false)

  | Typ_tlam(a, k1, t1) -> 
    let k1' = tc_kind env k1 in 
    let env' = Env.push_local_binding env (Env.Binding_typ(a, k1')) in 
    let t1', k2 = tc_typ env' t1 in 
    Typ_tlam(a, k1', t1'), Kind_tcon(Some a, k1', k2, false)

  | Typ_ascribed(t1, k1) -> 
    let k1' = tc_kind env k1 in 
    let t1' = tc_typ_check env t1 k1' in
    Typ_ascribed(t1', k1'), k1'

  | Typ_uvar(u, k1) -> 
    t, k1

  | Typ_meta (Meta_pos(t, r)) -> 
    let env' = Env.set_range env r in
    let t', k = tc_typ env' t in 
    Typ_meta(Meta_pos(t', r)), k

  | Typ_unknown -> 
    let k = Tc.Util.new_kvar env in 
    let t = Tc.Util.new_tvar env k in 
    t, k   

and tc_typ_check env t k : typ = 
  let t', k' = tc_typ env t in
  Tc.Util.keq env (Some t') k' k;
  t'       

and tc_exp env e : exp * typ = match e with
  | Exp_withinfo(e, _, p) -> 
    let env = Tc.Env.set_range env p in
    let e, t = tc_exp env e in
    Exp_withinfo(e, t, p), t
   
  | Exp_bvar x -> 
    let e, t = Tc.Util.maybe_instantiate env e <| Env.lookup_bvar env x in 
    check_expected_typ env e t 

  | Exp_fvar(v, dc) -> 
    let e, t = Tc.Util.maybe_instantiate env e <| Env.lookup_lid env v.v in
    if dc &&  not (Env.is_datacon env v.v) && not (Env.is_logic_function env v.v)
    then raise (Error(Util.format1 "Expected a data constructor; got %s" v.v.str, Tc.Env.get_range env))
    else check_expected_typ env e t

  | Exp_constant c -> 
    let t = Tc.Util.typing_const env c in
    check_expected_typ env e t

  | Exp_abs(x, t1, e1) -> 
    let destruct_expected_typ env = 
      let rec aux norm env t = match compress_typ t with
        | Typ_fun(xopt, targ, tres, implicit) -> 
            let tres = match xopt with 
              | None -> tres
              | Some y -> Util.subst_typ [Inr(y, Util.bvd_to_exp x targ)] tres in
            Some targ, Some tres, env, (fun x -> x)
        | Typ_univ(a, k, t) -> 
            let env = Tc.Env.push_local_binding env (Env.Binding_typ(a, k)) in 
            let targ, tres, env, gen = aux norm env t in
            targ, tres, env, (fun x -> Exp_tabs(a, k, gen x))
        | _ when not norm ->
          aux true env (Tc.Util.normalize env t)
        | _ -> raise (Error(Tc.Errors.expected_a_term_of_type_t_got_a_function t e, rng env)) in
      match Tc.Env.expected_typ env with
        | None -> None, None, env, (fun x -> x) 
        | Some t -> 
          let targ, tres, env, gen = aux false env t in
          targ, tres, env, (fun (x,_) -> gen x, t) in

    let targ, tres, env, gen = destruct_expected_typ env in 
    let tx, k1 = tc_typ env t1 in
    let _ = match targ with 
      | None -> ()
      | Some targ -> Tc.Util.teq env tx targ in (* yes, really want this to be teq *)
    let envbody = match tres with 
      | None -> env
      | Some t -> Tc.Env.set_expected_typ env t in 
    let envbody = instantiate_both envbody in
    let e1', tres = tc_exp (Env.push_local_binding envbody (Env.Binding_var(x, tx))) e1 in 
    let t = Typ_fun(Some x, tx, tres, false) in
    let e' = Exp_abs(x, tx, e1') in 
    gen (e', t)
   
  | Exp_tabs(a, k1, e1) -> 
    let k1' = tc_kind env k1 in 
    let env, topt = Env.clear_expected_typ env in 
    let karg, env' = match topt with 
      | Some t -> 
        (match Util.compress_typ t with 
          | Typ_univ(b, karg, tres) -> 
            Tc.Util.keq env None karg k1';
            let tres = Util.subst_typ [Inl(b, Util.bvd_to_typ a karg)] tres in
            karg, Env.set_expected_typ env tres
          | _ -> k1', env)
      | None -> 
        k1', env in
    let env' = instantiate_both env' in
    let e1', tres = tc_exp (Env.push_local_binding env' (Env.Binding_typ(a, karg))) e1 in 
    let t = Typ_univ(a, karg, tres) in
    Exp_tabs(a, karg, e1'), t

  | Exp_app(e1, e2, imp) -> 
    let env1, _ = Env.clear_expected_typ env in
    let e1', t1 = tc_exp ({env1 with Tc.Env.instantiate_vargs=not imp}) e1 in
    let xopt, env2, tres, e1' = match Tc.Util.destruct_function_typ env t1 e1' imp with 
      | Typ_fun(xopt, targ, tres, _), e1' -> 
        let env2 = Env.set_expected_typ env targ in
        xopt, env2, tres, e1'
      | _ -> failwith "impossible" in 
    let e2', t2 = tc_exp (instantiate_both env2) e2 in 
    let t = match xopt with 
      | None -> tres
      | Some y -> Util.subst_typ [Inr(y, e2')] tres in
    check_expected_typ env (Exp_app(e1', e2', imp)) t 
       
  | Exp_tapp(e1, t1) -> 
    let env1, _ = Env.clear_expected_typ env in 
    let e1', t_e1 = tc_exp ({env1 with Env.instantiate_targs=false}) e1 in 
    let t1', k = tc_typ env1 t1 in 
    begin match Tc.Util.destruct_poly_typ env1 t_e1 e1' with
      | Typ_univ(a, k', tres), e1' ->
          Tc.Util.keq env1 (Some t1) k k';
          let t = Util.subst_typ [Inl(a, t1')] tres in
          check_expected_typ env (Exp_tapp(e1', t1')) t  
      | _ -> failwith "impossible"
    end
    
  | Exp_match(e1, eqns) -> 
    let env1, topt = Env.clear_expected_typ env in 
    let env1 = instantiate_both env1 in
    let e1', t1 = tc_exp env1 e1 in
    let t_eqns = eqns |> List.map (tc_eqn t1 env) in
    let eqns', result_t = match topt with 
      | Some t -> (* already checked against type from context *)
        t_eqns |> List.map (fun (p, w, b, _) -> (p, w, b)), t
      | None -> 
        let result_t = Tc.Util.new_tvar env Kind_star in
        let eqns' = t_eqns |> List.map (fun (pat, w, b, tb) -> (pat, w, Tc.Util.check_and_ascribe env b tb result_t)) in
        eqns', result_t in
    Exp_match(e1', eqns'), result_t

  | Exp_ascribed(e1, t1) -> 
    let t1' = tc_typ_check env t1 Kind_star in 
    let env1 = Env.set_expected_typ env t1' in 
    let e1', _ = tc_exp env1 e1 in
    check_expected_typ env (Exp_ascribed(e1', t1')) t1'
  
  | Exp_let((false, [(x, Typ_unknown, e1)]), e2) -> 
    let env = instantiate_both env in
    let env1, topt = Env.clear_expected_typ env in 
    let e1', t1 = tc_exp env1 e1 in 
    let uvs = Tc.Env.uvars_in_env env in
    let e1', s1 = Tc.Util.generalize env1 (Env.uvars_in_env env1) e1' t1 in (* TODO: check for unresolved implicit and kind vars *)
    let env2 = match x with 
      | Inl bvd -> Env.push_local_binding env (Env.Binding_var(bvd, s1))
      | Inr l -> Env.push_local_binding env (Env.Binding_lid(l, s1)) in 
    let e2', t = tc_exp env2 e2 in 
    let e' = Exp_let((false, [(x, s1, e1')]), e2') in
    begin match topt, x with 
      | None, Inl bvd -> 
         let _, fxvs = Util.freevars_typ t in 
         if Util.for_some (fun y -> bvd_eq y.v bvd) fxvs
         then raise (Error(Tc.Errors.inferred_type_causes_variable_to_escape t bvd, rng env))
         else e', t
      | _ -> e', t
    end       
  
  | Exp_let((false, [(x, t1, e1)]), e2) -> (* TODO: Check for unresolved implicits and kind vars *)
    let env = instantiate_both env in
    let t1' = tc_typ_check env t1 Kind_star in 
    let env1 = Env.set_expected_typ env t1' in   
    let e1', _ = tc_exp env1 e1 in 
    let env2 = match x with 
      | Inl bvd -> Env.push_local_binding env (Env.Binding_var(bvd, t1'))
      | Inr l -> Env.push_local_binding env (Env.Binding_lid(l, t1')) in
    let e2', t = tc_exp env2 e2 in
    Exp_let((false, [(x, t1', e1')]), e2'), t
    
  | Exp_let((false, _), _) -> 
    failwith "impossible"

  | Exp_let((true, lbs), e1) ->
    let env = instantiate_both env in
    let env0, topt = Env.clear_expected_typ env in 
    let lbs, env' = lbs |> List.fold_left (fun (xts, env) (x, t, e) -> 
      let t = match t with 
        | Typ_unknown -> Util.new_tvar env Kind_star
        | _ -> tc_typ_check env0 t Kind_star in
      let env = match x with 
        | Inl bvd -> Env.push_local_binding env (Env.Binding_var(bvd, t)) 
        | Inr l -> Env.push_local_binding env (Env.Binding_lid(l, t)) in
      (x, t, e)::xts, env) ([], env0)  in
    let lbs = lbs |> List.map (fun (x, t, e) -> 
      let env' = Env.set_expected_typ env' t in
      let e, _ = tc_exp env' e in 
      (x, t, e)) in  
    let lbs, env = lbs |> List.fold_left (fun (lbs, env) (x, t, e) -> 
      let e, t = Tc.Util.generalize env' (Env.uvars_in_env env') e t in
      let env = match x with 
        | Inl bvd -> Env.push_local_binding env (Env.Binding_var(bvd, t))
        | Inr l -> Env.push_local_binding env (Env.Binding_lid(l, t)) in
      (x, t, e)::lbs, env) ([], env) in
    let e1, t = tc_exp env e1 in 
    let res = Exp_let((true, lbs), e1) in
    begin match topt with 
      | Some _ -> res, t
      | None -> 
         let _, fxvs = Util.freevars_typ t in 
         match fxvs |> List.tryFind (fun y -> lbs |> Util.for_some (function
          | (Inr _, _, _) -> false
          | (Inl x, _, _) -> bvd_eq x y.v)) with
            | None -> res, t
            | Some y -> raise (Error(Tc.Errors.inferred_type_causes_variable_to_escape t y.v, rng env))
    end

  | Exp_primop(op, es) -> 
    let env = instantiate_both env in
    let op_t = Tc.Env.lookup_operator env op in
    let x = Util.new_bvd (Some op.idRange) in
    let env' = Tc.Env.push_local_binding env (Env.Binding_var(x, op_t)) in
    let app = Util.mk_curried_app (Util.bvd_to_exp x op_t) (List.map Inr es) in
    let app', t = tc_exp env' app in
    let _, tes = Util.uncurry_app app' in
    let es' = tes |> List.map (function 
      | Inl _ -> failwith "Impossible"
      | Inr e -> e) in 
    Exp_primop(op, es'), t

  | Exp_uvar(u, t1) -> 
    check_expected_typ env e t1

and tc_eqn pat_t env (pat, when_clause, branch) = 
  let env' = tc_pat pat_t env pat in 
  let when_clause' = match when_clause with 
    | None -> None
    | Some e -> Some (fst <| tc_exp (Env.set_expected_typ env' Tc.Util.t_bool) e) in
  let branch', t = tc_exp env' branch in 
  (pat, when_clause', branch', t)

and tc_pat (pat_t:typ) env p : Env.env = 
  let pvar_eq x y = match x, y with 
    | Inl xx, Inl yy -> bvd_eq xx yy
    | Inr xx, Inr yy -> bvd_eq xx yy
    | _ -> false in
  let var_exists vars x = vars |> Util.for_some (pvar_eq x) in
  let rec mk_pat_env vars env p = match p with 
    | Pat_wild 
    | Pat_twild
    | Pat_constant _ -> env, []
    | Pat_withinfo(p, _) -> mk_pat_env vars env p
    | Pat_var x -> 
      if var_exists vars (Inr x) 
      then raise (Error(Tc.Errors.nonlinear_pattern_variable x, Util.range_of_bvd x))
      else 
        let env = Tc.Env.push_local_binding env (Env.Binding_var(x, Tc.Util.new_tvar env Kind_star)) in
        env, [Inl x]
    | Pat_tvar a -> 
      if var_exists vars (Inl a) 
      then raise (Error(Tc.Errors.nonlinear_pattern_variable a, Util.range_of_bvd a))
      else 
        let env = Tc.Env.push_local_binding env (Env.Binding_typ(a, Tc.Util.new_kvar env)) in 
        env, [Inr a]
    | Pat_cons(l, pats) -> 
      pats |> List.fold_left (fun (env, outvars) p -> 
        let env, moreoutvars = mk_pat_env (outvars@vars) env p in
        env, moreoutvars@outvars) (env, [])
    | Pat_disj [] -> failwith "impossible"
    | Pat_disj (p::pats) -> 
      let env, outvars = mk_pat_env vars env p in 
      pats |> List.iter (fun p -> 
        let _, outvars' = mk_pat_env vars env p in
        if not (Util.multiset_equiv pvar_eq outvars outvars')
        then raise (Error(Tc.Errors.disjunctive_pattern_vars outvars outvars', Tc.Env.get_range env))
        else ());
      env, outvars in
  let pat_env, _ = mk_pat_env [] env p in
  let exps = Tc.Util.pat_as_exps env p in
  let env = {(Tc.Env.set_expected_typ pat_env pat_t) with Env.is_pattern=true} in
  //Printf.printf "Checking patterns ... expecting type %s\n" (Print.typ_to_string pat_t);
  ignore <| List.map (tc_exp env) exps; 
  pat_env

let tc_tparams env tps : (list<tparam> * Env.env) = 
	let tps', env = List.fold_left (fun (tps, env) tp -> match tp with 
	  | Tparam_typ(a, k) -> 
				let k = tc_kind env k in 
				let env = Tc.Env.push_local_binding env (Env.Binding_typ(a, k)) in
				Tparam_typ(a,k)::tps, env
	  | Tparam_term(x, t) -> 
				let t, _ = tc_typ env t in 
				let env = Tc.Env.push_local_binding env (Env.Binding_var(x, t)) in
				Tparam_term(x, t)::tps, env) ([], env) tps in
		List.rev tps', env 


let rec tc_decl env se = match se with 
    | Sig_tycon (lid, tps, k, _mutuals, _data, tags) -> 
      let env = Tc.Env.set_range env (range_of_lid lid) in 
      let tps, env = tc_tparams env tps in 
      let k = tc_kind env k in 
      let se = Sig_tycon(lid, tps, k, _mutuals, _data, tags) in  
      let _ = match compress_kind k with
        | Kind_uvar _ -> Tc.Util.keq env None k Kind_star
        | _ -> () in 
      let env = Tc.Env.push_sigelt env se in
      se, env
  
    | Sig_typ_abbrev(lid, tps, k, t) -> 
      let env = Tc.Env.set_range env (range_of_lid lid) in 
      let tps, env = tc_tparams env tps in
      let t, k1 = tc_typ env t in 
      let k2 = tc_kind env k in 
      Tc.Util.keq env (Some t) k1 k2;
      let se = Sig_typ_abbrev(lid, tps, k1, t) in 
      let env = Tc.Env.push_sigelt env se in 
      se, env
  
    | Sig_datacon(lid, t, tname) -> 
      let t = tc_typ_check env t Kind_star in 
      let args, result_t = Util.collect_formals t in
      let constructed_t, _ = Util.flatten_typ_apps result_t in (* TODO: check that the tps in tname are the same as here *)
      let _ = match constructed_t with
        | Typ_const fv when lid_equals fv.v tname -> ()
        | _ -> raise (Error (Tc.Errors.constructor_builds_the_wrong_type lid constructed_t tname, range_of_lid lid)) in
      let se = Sig_datacon(lid, t, tname) in 
      let env = Tc.Env.push_sigelt env se in 
      se, env
  
    | Sig_val_decl(lid, t, tag) -> 
      let t = tc_typ_check env t Kind_star in 
      let se = Sig_val_decl(lid, t, tag) in 
      let env = Tc.Env.push_sigelt env se in 
      se, env
  
    | Sig_assume(lid, phi, qual, tag) ->
      let phi = tc_typ_check env phi Kind_star in 
      let se = Sig_assume(lid, phi, qual, tag) in 
      let env = Tc.Env.push_sigelt env se in 
      se, env
  
    | Sig_logic_function(lid, t, tags) -> 
      let t = tc_typ_check env t Kind_star in 
      let se = Sig_logic_function(lid, t, tags) in 
      let env = Tc.Env.push_sigelt env se in 
      se, env
  
    | Sig_let lbs -> 
      let r, lbs' = snd lbs |> List.fold_left (fun (r, lbs) lb -> 
        let r = Range.union_ranges r (range_of_lb lb)  in
        let lb = match lb with 
          | (Inl _, _, _) -> failwith "impossible"
          | (Inr l, t, e) -> 
            match Tc.Env.try_lookup_val_decl env l with 
              | None -> (Inr l, t, e)
              | Some t' -> match t with 
                  | Typ_unknown -> 
                    (Inr l, t', e)
                  | _ -> raise (Error (Tc.Errors.inline_type_annotation_and_val_decl l, range_of_lid l)) in
        (r, lb::lbs)) (range_of_lb (List.hd (snd lbs)), []) in
      let lbs' = List.rev lbs' in
      let e = Exp_let((fst lbs, lbs'), Exp_constant(Syntax.Const_unit)) in
      let se = match fst <| tc_exp env e with 
        | Exp_let(lbs, _) -> Sig_let lbs
        | _ -> failwith "impossible" in
      let env = Tc.Env.push_sigelt env se in 
      se, env

    | Sig_main e -> 
      let env = Tc.Env.set_expected_typ env Util.t_unit in
      let e, _ = tc_exp env e in 
      let se = Sig_main e in 
      let env = Tc.Env.push_sigelt env se in 
      se, env

    | Sig_bundle ses -> 
      let tycons, rest = ses |> List.partition (function
        | Sig_tycon _ -> true
        | _ -> false) in
      let abbrevs, rest = rest |> List.partition (function 
        | Sig_typ_abbrev _ -> true
        | _ -> false) in
      let se = Sig_bundle (fst <| tc_decls env (tycons@abbrevs@rest)) in
      let env = Tc.Env.push_sigelt env se in 
      se, env
and tc_decls env ses = 
  let ses, env = List.fold_left (fun (ses, env) se ->
  //Printf.printf "Checking sigelt\n\t%s\n" (Print.sigelt_to_string se);
  let se, env = tc_decl env se in 
  //Printf.printf "Checked sigelt\n\t%s\n" (Print.sigelt_to_string se);
  se::ses, env) ([], env) ses in
  List.rev ses, env 

let tc_modul env modul = 
  let env = Tc.Env.set_current_module env modul.name in 
  let ses, env = tc_decls env modul.declarations in 
  let modul = {name=modul.name; declarations=ses; exports=[]} in (* TODO: handle exports *) 
  let env = Tc.Env.finish_module env modul in
  modul, env

let check_modules mods = 
   let fmods, _ = mods |> List.fold_left (fun (mods, env) m -> 
    let m, env = tc_modul env m in 
    (m::mods, env)) ([], Tc.Env.initial_env Const.prims_lid) in
   List.rev fmods
 
