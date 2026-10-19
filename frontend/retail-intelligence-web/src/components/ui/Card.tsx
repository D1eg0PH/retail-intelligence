import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

interface CardProps {
    children: ReactNode;
    className?: string;
}

export default function Card({
    children,
    className
}: CardProps) {
    return (
        <div
            className={cn(
                "rounded-xl border border-gray-200 bg-white p-6 shadow",
                className
            )}
        >
            {children}
        </div>
    );
}