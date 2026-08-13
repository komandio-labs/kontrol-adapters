#!/usr/bin/env python3
"""Generate ECDsa P-256 private and public key pair for Kontrol digital signing."""

import base64
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives import serialization

def generate_keypair():
    private_key = ec.generate_private_key(ec.SECP256R1())
    public_key = private_key.public_key()

    # Private key in PKCS#8 DER format -> Base64
    pri_der = private_key.private_bytes(
        encoding=serialization.Encoding.DER,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption()
    )
    pri_b64 = base64.b64encode(pri_der).decode('utf-8')

    # Public key in SubjectPublicKeyInfo DER format -> Base64
    pub_der = public_key.public_bytes(
        encoding=serialization.Encoding.DER,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    pub_b64 = base64.b64encode(pub_der).decode('utf-8')

    print("==========================================================")
    print("      KONTROL ECDSA (P-256) DIGITAL SIGNATURE KEYPAIR     ")
    print("==========================================================")
    print("\n[PRIVATE KEY] (Secret - Store in GitHub Secrets as KONTROL_SIGNING_PRIVATE_KEY):")
    print(f"{pri_b64}")
    print("\n[PUBLIC KEY] (Public - Embed in Kontrol.UI desktop app):")
    print(f"{pub_b64}")
    print("==========================================================")

    return pri_b64, pub_b64

if __name__ == "__main__":
    generate_keypair()
