import type { ReactNode } from "react";
import { cn } from "../../lib/utils";

interface PageHeaderProps {
    title: string;
    subtitle?: string;
    actions?: ReactNode;
    className?: string;
}

export default function PageHeader({
    title,
    subtitle,
    actions,
    className
}: PageHeaderProps) {
    return (
        <div
            className={cn(
                "mb-8 flex items-start justify-between gap-4",
                className
            )}
        >
            <div>
                <h1 className="text-3xl font-bold text-gray-900">
                    {title}
                </h1>

                {subtitle && (
                    <p className="mt-1 text-gray-500">
                        {subtitle}
                    </p>
                )}
            </div>

            {actions && (
                <div className="flex items-center gap-2">
                    {actions}
                </div>
            )}
        </div>
    );
}