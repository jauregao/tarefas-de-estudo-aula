import { HttpClient } from '@angular/common/http';
import { inject, Injectable, InjectionToken, signal } from '@angular/core';
import { environment } from '../environments/environment';
import { catchError, finalize, Observable, of, switchMap, tap, throwError } from 'rxjs';
import { AuthSessionStore } from './auth-session.store';

export interface Usuario {
  id: string;
  nome: string;
  email: string;
}

export interface Credenciais {
  email: string;
  senha: string;
}

export interface DadosCadastro extends Credenciais {
  nome: string;
}

interface TokenResposta {
  accessToken: string;
  refreshToken: string;
}

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root', factory: () => environment.apiBaseUrl,
});

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(AuthSessionStore);
  private readonly baseUrl = inject(API_BASE_URL);
  readonly usuario = signal<Usuario | null>(null);
  readonly carregando = signal(true);

  iniciarSessao(): Observable<Usuario | null> {
    if (!this.session.accessToken()) {
      this.carregando.set(false);
      return of(null);
    }

    return this.http.get<Usuario>(`${this.baseUrl}/api/auth/me`).pipe(
      tap(usuario => this.usuario.set(usuario)),
      catchError(() => {
        this.session.limpar();
        this.usuario.set(null);
        return of(null);
      }),
      finalize(() => this.carregando.set(false)),
    );
  }

  cadastrar(dados: DadosCadastro): Observable<Usuario> {
    return this.http.post<void>(`${this.baseUrl}/api/auth/register`, dados).pipe(
      switchMap(() => this.entrar({ email: dados.email, senha: dados.senha })),
    );
  }

  entrar(credenciais: Credenciais): Observable<Usuario> {
    return this.http.post<TokenResposta>(
      `${this.baseUrl}/api/identity/login?useCookies=false`,
      { email: credenciais.email, password: credenciais.senha },
    ).pipe(
      tap(resposta => this.session.salvarToken(resposta.accessToken)),
      switchMap(() => this.http.get<Usuario>(`${this.baseUrl}/api/auth/me`)),
      tap(usuario => this.usuario.set(usuario)),
      catchError(erro => {
        this.session.limpar();
        this.usuario.set(null);
        return throwError(() => erro);
      }),
    );
  }

  sair() {
    this.session.limpar();
    this.usuario.set(null);
  }
}
