import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/utils";

interface ButtonProps
    extends ButtonHTMLAttributes<HTMLButtonElement> {
    children: ReactNode;
}

export default function Button({
    children,
    className,
    ...props
}: ButtonProps) {
    return (
        <button
            {...props}
            className={cn(
                "px-4 py-2 rounded-lg bg-blue-600 text-white font-medium transition-colors hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed",
                className
            )}
        >
            {children}
        </button>
    );
}